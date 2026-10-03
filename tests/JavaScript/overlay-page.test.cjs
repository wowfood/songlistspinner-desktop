const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const assert = require('node:assert/strict');
const { test } = require('node:test');

const webRoot = path.resolve(__dirname, '../../src/SonglistSpinner.Desktop/wwwroot');
const overlayHtml = fs.readFileSync(path.join(webRoot, 'overlay/Overlay.html'), 'utf8');

function createElement(id = null) {
    const classes = new Set();
    const element = {
        id,
        style: { setProperty(name, value) { this[name] = value; } },
        dataset: {},
        children: [],
        textContent: '',
        hidden: false,
        classList: {
            add: name => classes.add(name),
            remove: name => classes.delete(name),
            toggle: (name, force) => (force ?? !classes.has(name)) ? classes.add(name) : classes.delete(name),
            contains: name => classes.has(name)
        },
        // Clearing the markup removes the children, as it does in a browser.
        set innerHTML(value) { this.children = []; this.textContent = value; },
        get innerHTML() { return this.textContent; },
        appendChild(child) { this.children.push(child); return child; },
        append(...children) { this.children.push(...children); },
        replaceChildren() { this.children = []; },
        setAttribute() { },
        getContext: () => ({ measureText: () => ({ width: 10 }) })
    };
    return element;
}

// Loads the overlay the way OBS does: the contracts, the wheel library, the shared interop and the
// page's inline scripts in one global scope, with only the elements Overlay.html declares.
// `search` is the page's query string; `reducedMotion` is what prefers-reduced-motion reports.
function loadOverlayPage({ search = '', reducedMotion = true } = {}) {
    const elements = new Map(
        [...overlayHtml.matchAll(/\bid="([^"]+)"/g)].map(match => [match[1], createElement(match[1])]));
    const windowListeners = {};
    const eventSources = [];
    const wheels = [];
    const timers = [];
    const errors = [];
    const context = {
        document: {
            getElementById: id => elements.get(id) ?? null,
            createElement: () => createElement(),
            body: createElement(),
            documentElement: createElement()
        },
        location: { search },
        URLSearchParams,
        addEventListener(type, listener) { windowListeners[type] = listener; },
        matchMedia: () => ({ matches: reducedMotion }),
        ResizeObserver: class { observe() { } disconnect() { } },
        EventSource: class {
            constructor(url) { this.url = url; this.listeners = {}; this.closed = false; eventSources.push(this); }
            addEventListener(type, listener) { this.listeners[type] = listener; }
            close() { this.closed = true; }
        },
        spinWheel: {
            Wheel: class {
                constructor(container, options) { this.items = options.items; this.spins = []; wheels.push(this); }
                spinToItem(index, duration) { this.spins.push({ index, duration }); }
                remove() { }
            }
        },
        setTimeout(callback, delay) { timers.push({ callback, delay }); },
        clearTimeout() { },
        performance: { now: () => 0 },
        console: { error: message => errors.push(message), warn() { }, log() { } }
    };
    context.window = context;
    vm.createContext(context);

    vm.runInContext(fs.readFileSync(path.join(webRoot, 'overlay/SongSpinner.contracts.js'), 'utf8'), context);
    vm.runInContext(fs.readFileSync(path.join(webRoot, 'spinner/SongSpinner.interop.js'), 'utf8'), context);
    for (const match of overlayHtml.matchAll(/<script>([\s\S]*?)<\/script>/g)) {
        vm.runInContext(match[1], context);
    }

    windowListeners.load();
    // Events go to the newest connection, which is the live one after a reconnect.
    const send = (eventName, data) => eventSources.at(-1).listeners[eventName]({ data: JSON.stringify(data) });
    const currentWheel = () => wheels.at(-1);
    return { elements, send, context, eventSources, currentWheel, timers, errors, windowListeners };
}

function initialState(overrides = {}) {
    return {
        config: {
            wheelColors: ['#ff6b6b'],
            colors: { text: '#ffffff' },
            playedList: { showFieldHeaders: false, position: 'right' },
            winnerDialog: {},
            background: { mode: 'color', color: '#111111' },
            nowPlaying: { enabled: false, position: 'bottom-left', width: '28rem', fontFamily: 'sans-serif', fontSize: '1.125rem' }
        },
        streamer: 'streamer',
        wheelItems: [{ queueId: 1, label: 'Artist - Title (Viewer)' }],
        playedTexts: [],
        playedFieldTable: null,
        nowPlayingText: null,
        playedCount: 0,
        availableCount: 1,
        playedListCollapsed: false,
        playedListWidth: '',
        playedListMinWidth: '',
        wheelVisible: true,
        winner: null,
        ...overrides
    };
}

test('Overlay.html loads the shared SpinnerInterop script instead of defining its own', () => {
    const scriptSources = [...overlayHtml.matchAll(/<script src="([^"]+)"><\/script>/g)].map(match => match[1]);

    assert.ok(scriptSources.includes('/overlay/SongSpinner.interop.js'));
    assert.equal(overlayHtml.includes('window.SpinnerInterop ='), false);
});

test('Overlay.html and the shared interop load together and define SpinnerInterop once', () => {
    const { context } = loadOverlayPage();

    assert.equal(typeof context.SpinnerInterop.createWheel, 'function');
});

test('Overlay: a collapsed played list on connect is hidden and keeps its CSS width', () => {
    const { elements, send } = loadOverlayPage();

    send('init_state', initialState({ playedListCollapsed: true }));

    const playedList = elements.get('playedList');
    assert.equal(playedList.classList.contains('collapsed'), true);
    assert.equal(playedList.style.width, undefined);
    assert.equal(playedList.style.minWidth, undefined);
});

test('Overlay: expanding after a width change keeps the replayed width', () => {
    const { elements, send } = loadOverlayPage();
    send('init_state', initialState({ playedListCollapsed: true }));

    send('set_played_list_width', { width: '40%', minWidth: '300px' });
    send('set_collapse', { collapsed: false });

    const playedList = elements.get('playedList');
    assert.equal(playedList.classList.contains('collapsed'), false);
    assert.equal(playedList.style.width, '40%');
    assert.equal(playedList.style.minWidth, '300px');
});

test('Overlay: a left played-list position moves the list without a collapse icon', () => {
    const { elements, send } = loadOverlayPage();
    const state = initialState();
    state.config.playedList.position = 'LEFT';

    send('init_state', state);

    assert.equal(elements.get('container').classList.contains('played-list-left'), true);
    assert.equal(elements.get('playedList').dataset.position, 'left');
});

test('Overlay: Now Playing uses the configured position, width and font', () => {
    const { elements, send } = loadOverlayPage();
    const state = initialState({ nowPlayingText: 'Artist: A' });
    state.config.nowPlaying = { enabled: true, position: 'top-right', width: '20rem', fontFamily: 'serif', fontSize: '2rem' };

    send('init_state', state);

    const nowPlaying = elements.get('nowPlaying');
    assert.equal(nowPlaying.hidden, false);
    assert.equal(nowPlaying.dataset.position, 'top-right');
    assert.equal(nowPlaying.style.width, '20rem');
    assert.equal(elements.get('nowPlayingText').style.fontFamily, 'serif');
    assert.equal(elements.get('nowPlayingText').style.fontSize, '2rem');
});

test('Overlay: a hidden wheel on connect stays hidden', () => {
    const { elements, send } = loadOverlayPage();

    send('init_state', initialState({ wheelVisible: false }));

    assert.equal(elements.get('wheelContents').style.display, 'none');
});

test('Overlay: a winner shown on connect is replayed with its fields and queue position', () => {
    const { elements, send } = loadOverlayPage();

    send('init_state', initialState({
        winner: { fields: [{ label: 'Title', value: 'Song One' }, { label: 'Requester', value: 'Viewer' }], queuePosition: 3 }
    }));

    assert.equal(elements.get('winnerModal').style.display, 'block');
    assert.deepEqual(
        elements.get('winnerFields').children.map(child => child.textContent),
        ['Title', 'Song One', '|', 'Requester', 'Viewer']);
    assert.equal(elements.get('winnerQueuePositionValue').textContent, '#3');
    assert.equal(elements.get('winnerQueuePosition').hidden, false);
});

test('Overlay: no winner on connect keeps the winner dialog closed', () => {
    const { elements, send } = loadOverlayPage();

    send('init_state', initialState({ winner: null }));

    assert.equal(elements.get('winnerModal').style.display, 'none');
});

test('Overlay: a spin command spins to the winner by queue id when the wheel order differs', () => {
    const { send, currentWheel } = loadOverlayPage();
    send('init_state', initialState({
        wheelItems: [{ queueId: 5, label: 'First' }, { queueId: 9, label: 'Second' }]
    }));

    send('spin_command', { winnerIndex: 0, winnerQueueId: 9, duration: 5000 });

    assert.deepEqual(currentWheel().spins, [{ index: 1, duration: 5000 }]);
});

test('Overlay: a spin command for a winner missing from the wheel does not spin', () => {
    const { send, currentWheel, errors } = loadOverlayPage();
    send('init_state', initialState({ wheelItems: [{ queueId: 5, label: 'First' }] }));

    send('spin_command', { winnerIndex: 0, winnerQueueId: 9, duration: 5000 });

    assert.deepEqual(currentWheel().spins, []);
    assert.deepEqual(errors, ['The selected winner is not present in the overlay wheel.']);
});

test('Overlay: a live winner reveal shows the winner and runs confetti in the wheel colours', () => {
    const { elements, send } = loadOverlayPage({ reducedMotion: false });
    const state = initialState();
    state.config.wheelColors = ['#111111', '#222222'];
    send('init_state', state);

    send('winner_reveal', { fields: [{ label: 'Title', value: 'Song One' }], queuePosition: 2 });

    assert.equal(elements.get('winnerModal').style.display, 'block');
    assert.deepEqual(elements.get('winnerFields').children.map(child => child.textContent), ['Title', 'Song One']);
    assert.equal(elements.get('winnerQueuePositionValue').textContent, '#2');
    const confetti = elements.get('winnerConfetti').children;
    assert.equal(confetti.length, 36);
    assert.deepEqual([...new Set(confetti.map(piece => piece.style.backgroundColor))], ['#111111', '#222222']);
});

test('Overlay: closing the winner hides it', () => {
    const { elements, send } = loadOverlayPage();
    send('init_state', initialState());
    send('winner_reveal', { fields: [{ label: 'Title', value: 'Song One' }], queuePosition: null });

    send('close_winner', {});

    assert.equal(elements.get('winnerModal').style.display, 'none');
});

test('Overlay: wheel visibility and played-list collapse apply while connected', () => {
    const { elements, send } = loadOverlayPage();
    send('init_state', initialState());

    send('set_wheel_visible', { visible: false });
    send('set_collapse', { collapsed: true });

    assert.equal(elements.get('wheelContents').style.display, 'none');
    assert.equal(elements.get('playedList').classList.contains('collapsed'), true);
});

test('Overlay: Now Playing stays hidden when the workflow is off or nothing is playing', () => {
    const { elements, send } = loadOverlayPage();
    const workflowOff = initialState({ nowPlayingText: 'Artist: A' });
    workflowOff.config.nowPlaying.enabled = false;
    const nothingPlaying = initialState({ nowPlayingText: null });
    nothingPlaying.config.nowPlaying.enabled = true;

    send('update_songs', workflowOff);
    const hiddenWhenOff = elements.get('nowPlaying').hidden;
    send('update_songs', nothingPlaying);

    assert.equal(hiddenWhenOff, true);
    assert.equal(elements.get('nowPlaying').hidden, true);
    assert.equal(elements.get('nowPlayingText').textContent, '');
});

test('Overlay: played songs with field headers render a numbered table under a header row', () => {
    const { elements, send } = loadOverlayPage();
    const state = initialState({
        playedFieldTable: {
            headers: ['Artist', 'Title'],
            rows: [{ number: 1, values: ['Artist A', 'Title A'] }],
            separator: ' / '
        }
    });
    state.config.playedList.showFieldHeaders = true;
    state.config.playedList.showNumbers = true;

    send('init_state', state);

    const rows = elements.get('playedSongsUl').children.map(row => row.children.map(cell => cell.textContent));
    assert.deepEqual(rows, [['#', 'Artist', ' / ', 'Title'], ['1.', 'Artist A', ' / ', 'Title A']]);
});

test('Overlay: a queue update replaces the played list and the song counts', () => {
    const { elements, send } = loadOverlayPage();
    send('init_state', initialState({ playedTexts: ['Old song'], playedCount: 1 }));

    send('update_songs', initialState({ playedTexts: ['Song A', 'Song B'], playedCount: 2, availableCount: 5 }));

    assert.deepEqual(
        elements.get('playedSongsUl').children.map(row => row.children[0].textContent),
        ['Song A', 'Song B']);
    assert.equal(elements.get('playedCount').textContent, 2);
    assert.equal(elements.get('availableCount').textContent, 5);
});

test('Overlay: a dropped event connection reconnects after three seconds and resynchronises', () => {
    const { elements, send, eventSources, timers } = loadOverlayPage();
    send('init_state', initialState({ streamer: 'before-drop' }));

    eventSources[0].onerror();
    const retry = timers.at(-1);
    retry.callback();
    send('init_state', initialState({ streamer: 'after-reconnect' }));

    assert.equal(eventSources[0].closed, true);
    assert.equal(retry.delay, 3000);
    assert.equal(eventSources.length, 2);
    assert.equal(eventSources[1].url, '/overlay/events');
    assert.equal(elements.get('streamerLabel').textContent, 'after-reconnect');
});

test('Overlay: the Settings preview ignores the event stream and applies only preview messages', () => {
    const { elements, context, eventSources, windowListeners } = loadOverlayPage({ search: '?preview=1' });
    const settingsPreview = context.SonglistSpinnerContracts.messageTypes.settingsPreview;

    windowListeners.message({ data: { type: 'something-else', payload: initialState({ streamer: 'ignored' }) } });
    windowListeners.message({ data: { type: settingsPreview, payload: initialState({ streamer: 'draft-channel' }) } });

    assert.equal(eventSources.length, 0);
    assert.equal(context.document.body.classList.contains('settings-preview-mode'), true);
    assert.equal(elements.get('streamerLabel').textContent, 'draft-channel');
});

test('Overlay: the live OBS overlay ignores Settings preview messages', () => {
    const { elements, context, send, windowListeners } = loadOverlayPage();
    send('init_state', initialState({ streamer: 'live-channel' }));

    windowListeners.message({
        data: {
            type: context.SonglistSpinnerContracts.messageTypes.settingsPreview,
            payload: initialState({ streamer: 'draft-channel' })
        }
    });

    assert.equal(elements.get('streamerLabel').textContent, 'live-channel');
});
test('Overlay: after the Dashboard changes streamer, the overlay stops showing the previous streamer', () => {
    const { elements, send } = loadOverlayPage();
    send('init_state', initialState({ streamer: 'previous-channel' }));

    send('update_songs', initialState({ streamer: '', wheelItems: [{ label: 'Waiting for Dashboard...' }], availableCount: 0 }));

    assert.equal(elements.get('streamerLabel').textContent, 'Waiting for Dashboard...');
});