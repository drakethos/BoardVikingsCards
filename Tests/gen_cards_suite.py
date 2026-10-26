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

# The tester is dressed (legs and chest) and the other mod's team glow is off, so the pictures look like a real player.
PREP = [
    {'do': 'setconfig', 'guid': 'com.drakemods.serverteams', 'values': {'4 Visuals/TeamGlow': False}},
    {'do': 'give', 'prefab': 'ArmorLeatherLegs', 'equip': True},
    {'do': 'give', 'prefab': 'ArmorLeatherChest', 'equip': True},
]


WEAR = [{'do': 'invoke', 'method': 'Cards.Table.TableTestApi.TestWearVisor', 'args': []}]


def add(title, steps, asserts=None, wear=None):
    global n
    if wear is None:
        wear = any(st.get('do') == 'screenshot' for st in steps) and 'table' not in title and 'visor' not in title
    w('C-%02d' % n, {'name': 'C-%02d %s' % (n, title), 'steps': [RANGE] + PREP + (WEAR if wear else []) + steps, 'asserts': (asserts or []) + no_err})
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
    {'do': 'screenshot', 'name': 'inventory-cards', 'clean': True},
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
    {'do': 'screenshot', 'name': 'giant-card', 'clean': True},
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
    {'do': 'wait', 'seconds': 0.5},
    inv(G, 'TestSetBack', 4), inv(G, 'TestSetFace', 'SA'),
    {'do': 'spawn', 'prefab': 'Card_Giant', 'dz': 4.5, 'dx': 0, 'as': 'b'},
    {'do': 'wait', 'seconds': 0.5},
    inv(G, 'TestSetBack', 3), inv(G, 'TestSetFace', 'HK'),
    {'do': 'spawn', 'prefab': 'Card_Giant', 'dz': 4, 'dx': 3, 'yaw': 180, 'as': 'c'},
    {'do': 'wait', 'seconds': 0.5},
    inv(G, 'TestSetBack', 1),
    {'do': 'wait', 'seconds': 1},
    {'do': 'lookAt', 'target': 'b', 'distance': 4},
    {'do': 'wait', 'seconds': 1},
    {'do': 'screenshot', 'name': 'giant-cards', 'clean': True},
])

add('giant card: the back design is chosen separately, and the next card copies the last pick', [
    inv(G, 'TestResetNext'),
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

add('paper brightness: the setting reaches the cards, stays in range, and a lit card does not glare', [
    inv(T, 'TestSetBrightness', 0.4), last(0.4, 'default brightness'),
    inv(T, 'TestSetBrightness', 5), last(1, 'never above 1'),
    inv(T, 'TestSetBrightness', 0), last(0.05, 'never fully black'),
    inv(T, 'TestSetBrightness', 0.4),
    {'do': 'spawn', 'prefab': 'Card_Giant', 'dz': 4, 'as': 'g'},
    {'do': 'wait', 'seconds': 0.5},
    inv(G, 'TestSetFace', 'CA'),
    {'do': 'lookAt', 'target': 'g', 'distance': 4},
    {'do': 'wait', 'seconds': 0.6},
    {'do': 'screenshot', 'name': 'brightness-0.4', 'clean': True},
    inv(T, 'TestSetBrightness', 0.25),
    {'do': 'wait', 'seconds': 0.4},
    {'do': 'screenshot', 'name': 'brightness-0.25', 'clean': True},
    inv(T, 'TestSetBrightness', 0.4),
])

add('giant card: face picker opens', [
    {'do': 'spawn', 'prefab': 'Card_Giant', 'dz': 4, 'as': 'g'},
    {'do': 'wait', 'seconds': 1},
    inv(G, 'TestOpenPicker'),
    {'do': 'wait', 'seconds': 1},
    {'do': 'screenshot', 'name': 'giant-picker', 'clean': True},
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
    {'do': 'screenshot', 'name': 'holding-card', 'clean': True},
    {'do': 'pressKey', 'button': 'Attack'},
    {'do': 'wait', 'seconds': 5},
    {'do': 'screenshot', 'name': 'after-throw', 'clean': True},
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
    {'do': 'lookAt', 'target': 't', 'distance': 3},
    {'do': 'wait', 'seconds': 0.6},
    {'do': 'screenshot', 'name': 'cards-in-target', 'clean': True},
    inv(T, 'TestTargetHits'), last(None, 'all three cards hit the target', min=3),
    inv(T, 'TestTargetScore'), last(None, 'they scored points', min=1),
    inv(T, 'TestTargetFaces'), last('SK,HQ,D7', 'the target remembers each face in order'),
    {'do': 'face', 'target': 't'},
    {'do': 'interact', 'target': 't'},
    {'do': 'wait', 'seconds': 1.5},
    inv(T, 'TestTargetHits'), last(0, 'pressing E collected them (score reset)'),
    {'do': 'screenshot', 'name': 'cards-returned', 'clean': True},
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


def table(owner=None, seats=None):
    step = {'do': 'spawn', 'prefab': 'Card_Table_Long', 'dz': 5, 'as': 't'}
    if owner:
        step['owner'] = owner
    steps = [step, {'do': 'wait', 'seconds': 1}]
    if seats:
        steps.append(inv(TB, 'TestSetSeats', seats))
    return steps


add('card table: Small and Long are two sizes of one table', [
    *table(),
    inv(TB, 'TestSize'), last('2.6,1.2', 'the long table'),
    {'do': 'destroy', 'target': 't'},
    {'do': 'spawn', 'prefab': 'Card_Table_Small', 'dz': 5, 'as': 's'},
    {'do': 'wait', 'seconds': 1},
    inv(TB, 'TestSize'), last('1.5,1.0', 'the small table'),
])

add('card table: the dealer sets 2 to 8 seats, players sit and leave', [
    *table(),
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

SCENARIOS = [
    ('deal', 'hands=5,5,5,5 deck=32 total=52'),
    ('deal-short', 'hands=2,1,1,1 deck=0'),
    ('play', 'hands=1,3,2,4 felt=2 discard=1 deck=39 up=ud total=52'),
    ('flip', 'own=True other=False creator=True visor=True'),
    ('turns', 'wrongTurn=False myTurn=True afterPass=True turn=1'),
    ('lock', 'creatorBefore=True locked=True creatorAfterLock=False playerSeats=False unlockWithoutVisor=False visorChanges=True visorUnlocks=True mode=turns locked=False'),
    ('locked-play', 'playerShuffle=False visorShuffle=True playerDraws=True'),
    ('no-creator', 'withoutVisor=False withVisor=True'),
    ('leave', 'taken=3 deck=37 total=52'),
    ('collect', 'deck=52 hands=0 felt=0 discard=0'),
    ('shuffle', 'changed=True total=52 distinct=52'),
    ('chips', 'start=270 bet=True tooMuch=False pot=20 afterWin=0 winner=280'),
    ('saved', 'same=True total=52 chips=267 pot=3 locked=True'),
]
steps = []
for name, expect in SCENARIOS:
    steps += [inv(TB, 'TestScenario', name), last(expect, 'scenario ' + name)]
add('card table rules on a bare state: deal, play, flip, turns, lock, leave, collect, shuffle, chips, saving', steps)

add('card table: put a deck on, deal, play, discard, draw, flip, collect, pick the deck up again', [
    *table(seats=4),
    inv(TB, 'TestSit', 1), last(1, 'sat in seat 1'),
    {'do': 'give', 'prefab': 'Card_Deck', 'count': 1},
    inv(TB, 'TestPlaceDeck'), last(52, 'the whole deck is on the table'),
    inv(TB, 'TestShuffle'), last(52, 'shuffled, still 52'),
    inv(TB, 'TestDeal', 5), last(47, 'dealt 5 to the one player seated'),
    inv(TB, 'TestHand'), last(5, 'my hand has 5 cards'),
    inv(TB, 'TestCardObjects'), last(None, 'the table shows its cards', min=6),
    inv(TB, 'TestPlay', 0, True), last(1, 'one card on the felt'),
    inv(TB, 'TestDiscard', 0), last(1, 'one card discarded'),
    inv(TB, 'TestDraw'), last(4, 'drew one: 5 - 2 + 1 cards in hand'),
    inv(TB, 'TestFlip', 0), last(True, 'turned a card face up'),
    inv(TB, 'TestCardTotal'), last(52, 'no card lost or added'),
    inv(TB, 'TestWearVisor'),
    {'do': 'lookAt', 'target': 't', 'distance': 3.2},
    {'do': 'wait', 'seconds': 0.5},
    {'do': 'screenshot', 'name': 'table-dealt', 'clean': True},
    inv(TB, 'TestGrab', 0), last(5, 'picked the played card back up'),
    inv(TB, 'TestCollect'), last(52, 'everything collected into the deck'),
    inv(TB, 'TestTakeDeck'), last(52, 'I picked the deck up: a deck item with 52 cards'),
    inv(TB, 'TestDeckCount'), last(0, 'the table has no deck now'),
], [{'check': 'hasItem', 'prefab': 'Card_Deck', 'count': 1, 'label': 'the deck is in my inventory again'}])

add('card table: the panel shows my hand, the piles and the dealer tools', [
    *table(seats=4),
    inv(TB, 'TestSit', 1),
    {'do': 'give', 'prefab': 'Card_Deck', 'count': 1},
    inv(TB, 'TestPlaceDeck'),
    inv(TB, 'TestDeal', 5),
    inv(TB, 'TestPlay', 0, True),
    inv(TB, 'TestOpenPanel'),
    {'do': 'wait', 'seconds': 0.8},
    {'do': 'screenshot', 'name': 'table-panel-hand', 'clean': True},
    {'do': 'pressKey', 'key': 'Escape'},
])

add('card table: it looks like a felt table with seats, a deck, cards and chips', [
    *table(seats=6),
    inv(TB, 'TestSit', 1),
    {'do': 'give', 'prefab': 'Card_Deck', 'count': 1},
    inv(TB, 'TestPlaceDeck'),
    inv(TB, 'TestDeal', 5),
    inv(TB, 'TestPlay', 0, True), inv(TB, 'TestPlay', 0, False), inv(TB, 'TestDiscard', 0),
    inv(TB, 'TestStack'), inv(TB, 'TestBet', 1, 3),
    inv(TB, 'TestTableLift'), last(None, 'its top is at about waist height above the ground', min=0.5, max=1.3),
    inv(TB, 'TestWearVisor'),
    {'do': 'lookAt', 'target': 't', 'distance': 3.2},
    {'do': 'wait', 'seconds': 0.8},
    {'do': 'screenshot', 'name': 'table', 'clean': True},
])

add('card table: the dealer visor makes the dealer; locked rules answer only to it', [
    *table(owner='Alice'),
    inv(TB, 'TestSit', 1),
    inv(TB, 'TestIsDealer'), last(False, 'not the dealer: Alice built the table and I wear no visor'),
    inv(TB, 'TestWearVisor'), last(True, 'the visor is on'),
    inv(TB, 'TestIsDealer'), last(True, 'wearing the visor makes me the dealer'),
    inv(TB, 'TestSetMode', 'turns'), last('turns', 'the dealer sets turn order'),
    inv(TB, 'TestLock', True), last('turns+locked', 'the dealer locks the rules'),
    inv(TB, 'TestTakeVisorOff'), last(True, 'the visor is off'),
    inv(TB, 'TestIsDealer'), last(False, 'no longer the dealer'),
    inv(TB, 'TestSetMode', 'free'), last('turns', 'locked rules refuse a player (the mode stays turns)'),
    inv(TB, 'TestWearVisor'),
    inv(TB, 'TestLock', False), last('turns', 'the visor unlocks them'),
])

add('dealer visor: how it looks on the head (front and side)', [
    inv(TB, 'TestWearVisor'), last(True, 'the visor is on'),
    {'do': 'wait', 'seconds': 1.5},
    inv(TB, 'TestCameraAround', 180),
    {'do': 'wait', 'seconds': 1.2},
    {'do': 'screenshot', 'name': 'visor-front', 'clean': True},
    inv(TB, 'TestCameraAround', 90),
    {'do': 'wait', 'seconds': 1.2},
    {'do': 'screenshot', 'name': 'visor-side', 'clean': True},
    inv(TB, 'TestCameraAround', 0),
])

add('card table: chips (a starting stack, a bet, the pot paid out)', [
    *table(seats=4),
    inv(TB, 'TestSit', 1),
    inv(TB, 'TestStack'), last(270, 'a starting stack is worth 270'),
    inv(TB, 'TestBet', 2, 2), last(50, 'two green chips in the pot (25 each)'),
    inv(TB, 'TestChips'), last(220, 'my stack is 270 - 50'),
    inv(TB, 'TestWin'), last(0, 'the pot is empty after paying it out'),
    inv(TB, 'TestChips'), last(270, 'and it all came back to me'),
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
    inv(T, 'TestGiveDeck', 7), last(True, 'a deck in the Sea back'),
    inv(T, 'TestDeckName'), last(None, 'named by its back', contains='Sea'),
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
    {'do': 'screenshot', 'name': 'decks-in-inventory', 'clean': True},
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
    {'do': 'screenshot', 'name': 'deck-window', 'clean': True},
    {'do': 'pressKey', 'key': 'Escape'},
])

print('wrote', n - 1, 'tests')
