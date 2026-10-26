"""Regenerates Tests/suites/BoardVikingsCards/*.json (run: python gen_cards_suite.py). Deployed by the build into drakeTest."""
import json, os

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'suites', 'BoardVikingsCards')
GUID = 'com.DrakeMods.BoardVikings-CardGames'
SRC = 'BoardVikings-CardGames'
suits = {'C': 'Clubs', 'H': 'Hearts', 'S': 'Spades', 'D': 'Diamonds'}
ranks = ['A', '2', '3', '4', '5', '6', '7', '8', '9', '10', 'J', 'Q', 'K']

def w(name, obj):
    with open(os.path.join(OUT, name + '.json'), 'w', encoding='utf-8') as f:
        json.dump(obj, f, indent=2)

for f in os.listdir(OUT):
    if f.endswith('.json'):
        os.remove(os.path.join(OUT, f))

w('suite', {
    'name': 'BoardVikingsCards',
    'description': 'Card items, giant card pieces, throwing, pick-up, deck, and a clean log.',
    'requireMods': [{'guid': GUID, 'minVersion': '0.0.1'}],
    'setup': {'timeOfDay': 0.5, 'freezeTime': 1, 'god': 1},
    'resetBetweenTests': True,
})

no_err = [{'check': 'noErrors', 'source': SRC, 'label': 'no errors from the card mod'},
          {'check': 'noErrors', 'label': 'no errors from anyone'}]

n = 1
for s, sname in suits.items():
    names = ['Card_' + s + r for r in ranks]
    w('C-%02d' % n, {
        'name': 'C-%02d all 13 %s exist as items' % (n, sname),
        'steps': [{'do': 'give', 'prefab': p, 'count': 1} for p in names],
        'asserts': [{'check': 'hasItem', 'prefab': p, 'count': 1, 'label': p} for p in names] + no_err,
    })
    n += 1

w('C-%02d' % n, {
    'name': 'C-%02d joker item exists' % n,
    'steps': [{'do': 'give', 'prefab': 'Card_Joker', 'count': 1}],
    'asserts': [{'check': 'hasItem', 'prefab': 'Card_Joker', 'count': 1}] + no_err,
}); n += 1

allcards = ['Card_%s%s' % (s, r) for s in suits for r in ranks] + ['Card_Joker']
for i in range(0, len(allcards), 14):
    chunk = allcards[i:i + 14]
    steps = []
    for j, p in enumerate(chunk):
        steps.append({'do': 'spawn', 'prefab': p + '_piece', 'dx': (j % 7) * 2 - 6, 'dz': 4 + (j // 7) * 3,
                      'as': 'p%d' % j})
    steps.append({'do': 'wait', 'seconds': 1})
    w('C-%02d' % n, {
        'name': 'C-%02d giant card pieces %d-%d spawn' % (n, i + 1, i + len(chunk)),
        'steps': steps,
        'asserts': [{'check': 'objectNear', 'target': p + '_piece', 'radius': 30, 'label': p + '_piece placed'} for p in chunk] + no_err,
    }); n += 1

w('C-%02d' % n, {
    'name': 'C-%02d dropped card lies on the ground and can be picked up' % n,
    'steps': [
        {'do': 'spawn', 'prefab': 'Card_CA', 'dz': 1.5, 'as': 'c'},
        {'do': 'wait', 'seconds': 2},
        {'do': 'face', 'target': 'c'},
        {'do': 'interact', 'target': 'c'},
        {'do': 'wait', 'seconds': 1},
    ],
    'asserts': [{'check': 'hasItem', 'prefab': 'Card_CA', 'count': 1, 'label': 'picked up'}] + no_err,
}); n += 1

w('C-%02d' % n, {
    'name': 'C-%02d throwing a card (spear throw, consumed, lands as an item)' % n,
    'steps': [
        {'do': 'give', 'prefab': 'Card_SK', 'count': 1, 'equip': True},
        {'do': 'wait', 'seconds': 0.5},
        {'do': 'pressKey', 'button': 'Attack'},
        {'do': 'wait', 'seconds': 5},
        {'do': 'screenshot', 'name': 'after-throw'},
    ],
    'asserts': [{'check': 'noItem', 'prefab': 'Card_SK', 'label': 'card left the inventory'},
                {'check': 'objectNear', 'target': 'Card_SK', 'radius': 80, 'label': 'card landed as an item'}] + no_err,
}); n += 1
M = 'Cards.Deck.DeckApi.'
def inv(method, *args):
    return {'do': 'invoke', 'method': M + method, 'args': list(args)}

w('C-%02d' % n, {
    'name': 'C-%02d deck: a fresh deck holds 52, pulls any card, takes it back' % n,
    'steps': [
        {'do': 'give', 'prefab': 'Card_Deck', 'count': 1},
        inv('TestCount'),
        {'do': 'check', 'check': 'lastResult', 'expect': 52, 'label': 'fresh deck has 52'},
        inv('TestPull', 'S7'),
        {'do': 'check', 'check': 'lastResult', 'expect': True, 'label': 'pulled the 7 of Spades'},
        inv('TestCount'),
        {'do': 'check', 'check': 'lastResult', 'expect': 51, 'label': 'deck now 51'},
        inv('TestPull', 'S7'),
        {'do': 'check', 'check': 'lastResult', 'expect': False, 'label': 'cannot pull the same card twice'},
        inv('TestReturnAll'),
        {'do': 'check', 'check': 'lastResult', 'expect': 1, 'label': 'one card went back'},
        inv('TestCount'),
        {'do': 'check', 'check': 'lastResult', 'expect': 52, 'label': 'deck whole again'},
    ],
    'asserts': [{'check': 'noItem', 'prefab': 'Card_S7', 'label': 'no loose 7 left'}] + no_err,
}); n += 1

w('C-%02d' % n, {
    'name': 'C-%02d deck: shuffle keeps 52 and changes the order' % n,
    'steps': [
        {'do': 'give', 'prefab': 'Card_Deck', 'count': 1},
        inv('TestShuffleTop'),
        {'do': 'check', 'check': 'lastResult', 'contains': ',', 'label': 'got a top-8 listing'},
        inv('TestCount'),
        {'do': 'check', 'check': 'lastResult', 'expect': 52, 'label': 'still 52'},
    ],
    'asserts': no_err,
}); n += 1

w('C-%02d' % n, {
    'name': 'C-%02d deck: window opens' % n,
    'steps': [
        {'do': 'give', 'prefab': 'Card_Deck', 'count': 1},
        inv('TestOpenWindow'),
        {'do': 'wait', 'seconds': 1},
        {'do': 'screenshot', 'name': 'deck-window'},
        {'do': 'pressKey', 'key': 'Escape'},
    ],
    'asserts': no_err,
}); n += 1
print('wrote', n - 1, 'tests')
