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
        innerHTML: '',
        hidden: false,
        classList: {
            add: name => classes.add(name),
            remove: name => classes.delete(name),
            toggle: (name, force) => (force ?? !classes.has(name)) ? classes.add(name) : classes.delete(name),
            contains: name => classes.has(name)
        },
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
function loadOverlayPage() {
    const elements = new Map(
        [...overlayHtml.matchAll(/\bid="([^"]+)"/g)].map(match => [match[1], createElement(match[1])]));
    const windowListeners = {};
    const eventSources = [];
    const context = {
        document: {
            getElementById: id => elements.get(id) ?? null,
            createElement: () => createElement(),
            body: createElement(),
            documentElement: createElement()
        },
        location: { search: '' },
        URLSearchParams,
        addEventListener(type, listener) { windowListeners[type] = listener; },
        matchMedia: () => ({ matches: true }),
        ResizeObserver: class { observe() { } disconnect() { } },
        EventSource: class {
            constructor(url) { this.url = url; this.listeners = {}; eventSources.push(this); }
            addEventListener(type, listener) { this.listeners[type] = listener; }
            close() { }
        },
        spinWheel: {
            Wheel: class {
                constructor(container, options) { this.items = options.items; }
                remove() { }
            }
        },
        setTimeout() { },
        clearTimeout() { },
        performance: { now: () => 0 },
        console
    };
    context.window = context;
    vm.createContext(context);

    vm.runInContext(fs.readFileSync(path.join(webRoot, 'overlay/SongSpinner.contracts.js'), 'utf8'), context);
    vm.runInContext(fs.readFileSync(path.join(webRoot, 'spinner/SongSpinner.interop.js'), 'utf8'), context);
    for (const match of overlayHtml.matchAll(/<script>([\s\S]*?)<\/script>/g)) {
        vm.runInContext(match[1], context);
    }

    windowListeners.load();
    const events = eventSources[0];
    const send = (eventName, data) => events.listeners[eventName]({ data: JSON.stringify(data) });
    return { elements, send, context };
}

function initialState(overrides = {}) {
    return {
        config: {
            wheelColors: ['#ff6b6b'],
            colors: { text: '#ffffff' },
            playedList: { showFieldHeaders: false },
            winnerDialog: {},
            background: { mode: 'color', color: '#111111' },
            songList: { playedListPosition: 'right' },
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
    state.config.songList.playedListPosition = 'LEFT';

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
