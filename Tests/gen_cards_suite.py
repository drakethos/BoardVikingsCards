"""Regenerates Tests/suites/BoardVikingsCards/*.json (run: python gen_cards_suite.py). Deployed by the build into drakeTest."""
import json, os

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'suites', 'BoardVikingsCards')
GUID = 'com.DrakeMods.BoardVikings-CardGames'
SRC = 'BoardVikings-CardGames'
suits = {'C': 'Clubs', 'H': 'Hearts', 'S': 'Spades', 'D': 'Diamonds'}
ranks = ['A', '2', '3', '4', '5', '6', '7', '8', '9', '10', 'J', 'Q', 'K']

os.makedirs(OUT, exist_ok=True)


def w(name, obj):
    with open(os.path.join(OUT, name + '.json'), 'w', encoding='utf-8') as f:
        json.dump(obj, f, indent=2)


for f in os.listdir(OUT):
    if f.endswith('.json'):
        os.remove(os.path.join(OUT, f))

w('suite', {
    'name': 'BoardVikingsCards',
    'description': 'Throwing and playing cards (one item, face = variant), icons, the deck, giant cards, throwing, and a clean log.',
    'requireMods': [{'guid': GUID, 'minVersion': '0.0.1'}],
    'setup': {'timeOfDay': 0.5, 'freezeTime': 1, 'god': 1},
    'resetBetweenTests': True,
})

no_err = [{'check': 'noErrors', 'source': SRC, 'label': 'no errors from the card mod'},
          {'check': 'noErrors', 'label': 'no errors from anyone'}]

T = 'Cards.CardsTestApi.'
D = 'Cards.Deck.DeckApi.'
G = 'Cards.Giant.GiantCardApi.'


def inv(cls, method, *args):
    return {'do': 'invoke', 'method': cls + method, 'args': list(args)}


def last(expect=None, label=None, **kw):
    c = {'do': 'check', 'check': 'lastResult', 'label': label}
    if expect is not None:
        c['expect'] = expect
    c.update(kw)
    return c


n = 1


# The test world starts inside a ring of standing stones, which blocks throws and hides things. Every test starts on open,
# flat ground to the south-west instead, facing down a long clear line.
RANGE = {'do': 'teleport', 'x': -6, 'z': -8.5, 'yaw': 218}


def add(title, steps, asserts=None):
    global n
    w('C-%02d' % n, {'name': 'C-%02d %s' % (n, title), 'steps': [RANGE] + steps, 'asserts': (asserts or []) + no_err})
    n += 1


# ---- one throwing-card item, 53 faces as variants
for s, sname in suits.items():
    steps = []
    for r in ranks:
        steps += [inv(T, 'TestGive', s + r, 'throw'), last(True, 'throwing ' + s + r)]
    add('throwing cards: all 13 %s can be made as variants of one item' % sname, steps)

add('throwing card: the Joker', [inv(T, 'TestGive', 'Joker', 'throw'), last(True, 'Joker'),
                                 inv(T, 'TestCarriedFace', 'throw'), last('Joker', 'face kept')])

# ---- icons and size
add('icons are square (never stretched) and the card is real-card shaped and small in hand', [
    inv(T, 'TestSquareIcons', 'throw'), last(53, 'all 53 throwing icons square'),
    inv(T, 'TestSquareIcons', 'play'), last(53, 'all 53 playing icons square'),
    inv(T, 'TestCardShape'), last(None, 'card is about 5:7 (0.6 - 0.8)', min=0.6, max=0.8),
    inv(T, 'TestHeldSize'), last(None, 'held card is at most 30 cm', min=0.05, max=0.3),
])

add('inventory shows the cards and the deck box in proportion', [
    {'do': 'give', 'prefab': 'Card_Deck', 'count': 1},
    inv(T, 'TestGive', 'S7', 'throw'), inv(T, 'TestGive', 'HK', 'throw'), inv(T, 'TestGive', 'DA', 'throw'),
    inv(T, 'TestGive', 'C10', 'throw'), inv(T, 'TestGive', 'Joker', 'throw'), inv(T, 'TestGive', 'H3', 'play'),
    {'do': 'pressKey', 'button': 'Inventory'},
    {'do': 'wait', 'seconds': 1},
    {'do': 'screenshot', 'name': 'inventory-cards'},
    {'do': 'closeUi'},
])

add('a card is named by its face', [
    inv(T, 'TestGive', 'S7', 'throw'),
    inv(T, 'TestCarriedName', 'throw'), last('Seven of Spades'.replace('Seven', '7'), 'name is the face'),
])

# ---- giant card
add('giant card: one piece, starts on its back, face can be changed and is saved', [
    {'do': 'spawn', 'prefab': 'Card_Giant', 'dz': 4, 'as': 'g'},
    {'do': 'wait', 'seconds': 1},
    inv(G, 'TestFace'), last('back', 'a new giant card shows its back'),
    inv(G, 'TestSetFace', 'S7'), last(True, 'face set'),
    {'do': 'wait', 'seconds': 1},
    inv(G, 'TestFace'), last('S7', 'the face is the 7 of Spades'),
    {'do': 'lookAt', 'target': 'g', 'distance': 3},
    {'do': 'wait', 'seconds': 0.5},
    {'do': 'screenshot', 'name': 'giant-card'},
], [{'check': 'objectNear', 'target': 'Card_Giant', 'radius': 30, 'label': 'giant card placed'}])

add('giant card: tilt leans the card without turning it, and the build-mode patches are active', [
    inv(G, 'TestTiltPatched'), last(True, 'tilt is hooked into build mode'),
    inv(G, 'TestOrientationLean', 0, 30), last(30, 'leans 30 degrees'),
    inv(G, 'TestOrientationLean', 90, 45), last(45, 'leans 45 degrees at any facing'),
    inv(G, 'TestOrientationYaw', 90, 40), last(90, 'still faces the same way when tilted'),
    inv(G, 'TestOrientationYaw', 200, 40), last(200, 'still faces the same way (other facing)'),
])

add('giant cards look matte and the text reads the right way round from both sides', [
    {'do': 'spawn', 'prefab': 'Card_Giant', 'dz': 5, 'dx': -3, 'as': 'a'},
    {'do': 'spawn', 'prefab': 'Card_Giant', 'dz': 5, 'dx': 0, 'as': 'b'},
    {'do': 'spawn', 'prefab': 'Card_Giant', 'dz': 5, 'dx': 3, 'yaw': 180, 'as': 'c'},
    {'do': 'wait', 'seconds': 1},
    inv(G, 'TestSetFace', 'HK'),
    {'do': 'lookAt', 'target': 'b', 'distance': 6},
    {'do': 'wait', 'seconds': 1},
    {'do': 'screenshot', 'name': 'giant-cards'},
])

add('giant card: the back design is chosen separately, and the next card copies the last pick', [
    {'do': 'spawn', 'prefab': 'Card_Giant', 'dz': 5, 'as': 'first'},
    {'do': 'wait', 'seconds': 1},
    inv(G, 'TestBack'), last(3, 'a new card starts with the default back (red lattice)'),
    inv(G, 'TestSetBack', 10), last(True, 'back picked'),
    inv(G, 'TestBack'), last(10, 'back is design 10'),
    inv(G, 'TestNextBack'), last(10, 'the next card will copy it'),
    {'do': 'spawn', 'prefab': 'Card_Giant', 'dz': 2, 'dx': 2, 'as': 'second'},
    {'do': 'wait', 'seconds': 1},
    inv(G, 'TestBack'), last(10, 'the card placed next has the same back'),
])

add('giant card: face picker opens', [
    {'do': 'spawn', 'prefab': 'Card_Giant', 'dz': 4, 'as': 'g'},
    {'do': 'wait', 'seconds': 1},
    inv(G, 'TestOpenPicker'),
    {'do': 'wait', 'seconds': 1},
    {'do': 'screenshot', 'name': 'giant-picker'},
    {'do': 'pressKey', 'key': 'Escape'},
])

# ---- ground and throw
add('dropped card lies on the ground and can be picked up', [
    {'do': 'spawn', 'prefab': 'Card_Throwing', 'dz': 1.5, 'as': 'c'},
    {'do': 'wait', 'seconds': 2},
    {'do': 'face', 'target': 'c'},
    {'do': 'interact', 'target': 'c'},
    {'do': 'wait', 'seconds': 1},
], [{'check': 'hasItem', 'prefab': 'Card_Throwing', 'count': 1, 'label': 'picked up'}])

add('throwing a card: it flies a spear-like distance, keeps its face, and lands as an item', [
    inv(T, 'TestProbeThrow', True),
    inv(T, 'TestEquipThrowing', 'SK'), last(True, 'holding the King of Spades'),
    {'do': 'wait', 'seconds': 1},
    {'do': 'screenshot', 'name': 'holding-card'},
    {'do': 'pressKey', 'button': 'Attack'},
    {'do': 'wait', 'seconds': 5},
    {'do': 'screenshot', 'name': 'after-throw'},
    inv(T, 'TestThrowDistance'), last(None, 'flew at least 5 m (not dropped at my feet)', min=5),
    inv(T, 'TestLandedFace'), last('SK', 'the landed card is still the King of Spades'),
], [{'check': 'noItem', 'prefab': 'Card_Throwing', 'label': 'card left the inventory'}])

# ---- the archery target
def throw_at_target(code):
    return [inv(T, 'TestEquipThrowing', code), {'do': 'wait', 'seconds': 0.6},
            {'do': 'lookAt', 'target': 't', 'distance': 5}, {'do': 'wait', 'seconds': 0.3},
            {'do': 'pressKey', 'button': 'Attack'}, {'do': 'wait', 'seconds': 2.2}]


add('archery target: it takes throwing cards as ammo', [
    {'do': 'spawn', 'prefab': 'piece_ArcheryTarget', 'dz': 4, 'as': 't'},
    {'do': 'wait', 'seconds': 1},
    inv(T, 'TestTargetAcceptsCards'), last(True, 'the target lists the throwing card as ammo'),
    inv(T, 'TestTargetHeight'), last(None, 'the bullseye stands above the ground, not buried or floating', min=0.5, max=3),
])

add('archery target: thrown cards score, stay stuck in it, and come back with their own faces', [
    inv(T, 'TestClearThrown'),
    {'do': 'spawn', 'prefab': 'piece_ArcheryTarget', 'dz': 4, 'as': 't'},
    {'do': 'wait', 'seconds': 1},
    *throw_at_target('SK'), *throw_at_target('HQ'), *throw_at_target('D7'),
    {'do': 'screenshot', 'name': 'cards-in-target'},
    inv(T, 'TestTargetHits'), last(None, 'all three cards hit the target', min=3),
    inv(T, 'TestTargetScore'), last(None, 'they scored points', min=1),
    inv(T, 'TestTargetFaces'), last('SK,HQ,D7', 'the target remembers each face in order'),
    {'do': 'face', 'target': 't'},
    {'do': 'interact', 'target': 't'},
    {'do': 'wait', 'seconds': 1.5},
    inv(T, 'TestTargetHits'), last(0, 'pressing E collected them (score reset)'),
    {'do': 'screenshot', 'name': 'cards-returned'},
    inv(T, 'TestNearbyFaces', 12), last('D7,HQ,SK', 'the same three cards came back, each with its own face'),
])

add('archery target: aim is honed (five throws, at least four hit)', [
    {'do': 'spawn', 'prefab': 'piece_ArcheryTarget', 'dz': 4, 'as': 't'},
    {'do': 'wait', 'seconds': 1},
    {'do': 'face', 'target': 't'},
    *throw_at_target('CA'), *throw_at_target('C2'), *throw_at_target('C3'), *throw_at_target('C4'), *throw_at_target('C5'),
    inv(T, 'TestTargetHits'), last(None, 'at least four of five hit', min=4),
])

# ---- the card table
TB = 'Cards.Table.TableTestApi.'

add('card table: Small and Long are two sizes of one table', [
    {'do': 'spawn', 'prefab': 'Card_Table_Long', 'dz': 5, 'as': 't'},
    {'do': 'wait', 'seconds': 1},
    inv(TB, 'TestSize'), last('2.6,1.2', 'the long table'),
    {'do': 'destroy', 'target': 't'},
    {'do': 'spawn', 'prefab': 'Card_Table_Small', 'dz': 5, 'as': 's'},
    {'do': 'wait', 'seconds': 1},
    inv(TB, 'TestSize'), last('1.5,1.0', 'the small table'),
])

add('card table: actions work on a bare state and survive saving', [
    inv(TB, 'TestActionsSelfCheck'),
    last(None, 'sit, save, reload, change seats', contains='sat=True seat=1 roundtrip=1 seats=True/6'),
])

add('card table: the dealer sets 2 to 8 seats, players sit and leave', [
    {'do': 'spawn', 'prefab': 'Card_Table_Long', 'dz': 5, 'as': 't'},
    {'do': 'wait', 'seconds': 1},
    inv(TB, 'TestSeats'), last(4, 'a new table has 4 seats'),
    inv(TB, 'TestSetSeats', 6), last(6, 'the dealer sets 6'),
    inv(TB, 'TestSetSeats', 12), last(8, 'never more than 8'),
    inv(TB, 'TestSetSeats', 1), last(2, 'never fewer than 2'),
    inv(TB, 'TestSit', 2), last(2, 'sat in seat 2'),
    inv(TB, 'TestTaken'), last(1, 'one seat taken'),
    inv(TB, 'TestSeatName', 2), last(None, 'the seat shows my name', contains='Bjorn'),
    inv(TB, 'TestSetSeats', 8),
    inv(TB, 'TestSit', 8), last(8, 'moved to seat 8'),
    inv(TB, 'TestTaken'), last(1, 'still one seat taken (moving frees the old one)'),
    inv(TB, 'TestSetSeats', 2), last(2, 'seats switched off'),
    inv(TB, 'TestSeatOf'), last(0, 'my switched-off seat sent me away'),
    inv(TB, 'TestSit', 1), last(1, 'sat in seat 1'),
    inv(TB, 'TestLeave'), last(0, 'left the table'),
    inv(TB, 'TestTaken'), last(0, 'empty again'),
])

add('card table: it looks like a felt table with seat markers, and the panel opens', [
    {'do': 'spawn', 'prefab': 'Card_Table_Long', 'dz': 5, 'as': 't'},
    {'do': 'wait', 'seconds': 1},
    inv(TB, 'TestSetSeats', 6),
    inv(TB, 'TestSit', 1),
    inv(TB, 'TestTableInfo'), last(None, 'the table stands near the ground in front of me (not floating)', contains='pos+'),
    inv(TB, 'TestTableLift'), last(None, 'its top is at about waist height above the ground', min=0.5, max=1.3),
    {'do': 'wait', 'seconds': 0.6},
    {'do': 'screenshot', 'name': 'table'},
    inv(TB, 'TestOpenPanel'),
    {'do': 'wait', 'seconds': 0.8},
    {'do': 'screenshot', 'name': 'table-panel'},
    {'do': 'pressKey', 'key': 'Escape'},
])

# ---- deck (playing cards)
add('deck: a fresh deck holds 52, pulls any card as a playing card, takes it back', [
    {'do': 'give', 'prefab': 'Card_Deck', 'count': 1},
    inv(D, 'TestCount'), last(52, 'fresh deck has 52'),
    inv(D, 'TestPull', 'S7'), last(True, 'pulled the 7 of Spades'),
    inv(D, 'TestCount'), last(51, 'deck now 51'),
    inv(T, 'TestCarriedFace', 'play'), last('S7', 'it is a playing card with the right face'),
    inv(D, 'TestPull', 'S7'), last(False, 'cannot pull the same card twice'),
    inv(D, 'TestReturnAll'), last(1, 'one card went back'),
    inv(D, 'TestCount'), last(52, 'deck whole again'),
    inv(T, 'TestCarriedFace', 'play'), last('', 'no playing card left'),
])

add('deck: the back design is a variant; the deck is named by it and its icons are square', [
    inv(T, 'TestDeckIcons'), last(15, 'one square deck icon per back design'),
    inv(T, 'TestGiveDeck', 7), last(True, 'a deck in the Blue Plain back'),
    inv(T, 'TestDeckName'), last(None, 'named by its back', contains='Blue Plain'),
])

add('deck: cards pulled from a deck carry the deck back', [
    inv(T, 'TestGiveDeck', 11), last(True, 'a deck in the Blue Diamonds back'),
    inv(D, 'TestPull', 'H5'), last(True, 'pulled the 5 of Hearts'),
    inv(T, 'TestCarriedBack', 'play'), last(11, 'the card has the deck back'),
])

add('inventory shows decks in different backs', [
    inv(T, 'TestGiveDeck', 0), inv(T, 'TestGiveDeck', 3), inv(T, 'TestGiveDeck', 5), inv(T, 'TestGiveDeck', 9),
    inv(T, 'TestGiveDeck', 13), inv(T, 'TestGiveDeck', 14),
    inv(T, 'TestGive', 'SQ', 'throw'),
    {'do': 'pressKey', 'button': 'Inventory'},
    {'do': 'wait', 'seconds': 1},
    {'do': 'screenshot', 'name': 'decks-in-inventory'},
    {'do': 'closeUi'},
])

add('deck: throwing cards never go into a deck', [
    {'do': 'give', 'prefab': 'Card_Deck', 'count': 1},
    inv(T, 'TestGive', 'S7', 'throw'),
    inv(D, 'TestPull', 'H2'), last(True, 'pulled the 2 of Hearts'),
    inv(D, 'TestReturnAll'), last(1, 'only the playing card went back'),
    inv(T, 'TestCarriedFace', 'throw'), last('S7', 'the throwing card stayed with me'),
])

add('deck: shuffle keeps 52 and changes the order', [
    {'do': 'give', 'prefab': 'Card_Deck', 'count': 1},
    inv(D, 'TestShuffleTop'), last(None, 'got a top-8 listing', contains=','),
    inv(D, 'TestCount'), last(52, 'still 52'),
])

add('deck: window opens', [
    {'do': 'give', 'prefab': 'Card_Deck', 'count': 1},
    inv(D, 'TestOpenWindow'),
    {'do': 'wait', 'seconds': 1},
    {'do': 'screenshot', 'name': 'deck-window'},
    {'do': 'pressKey', 'key': 'Escape'},
])

print('wrote', n - 1, 'tests')
