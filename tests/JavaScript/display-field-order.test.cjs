const path = require('node:path');
const { pathToFileURL } = require('node:url');
const assert = require('node:assert/strict');
const { test } = require('node:test');

// The Settings field-order editor's drag handler. The file is an ES module, so it is imported rather than
// required; it reads window.Sortable when initialize runs.
const modulePath = path.resolve(__dirname, '../../src/SonglistSpinner.Desktop/wwwroot/settings/DisplayFieldOrder.interop.js');

function createChip(fieldName) {
    return { dataset: { fieldName }, matches: selector => selector === '.ss-chip' };
}

function createList(...chips) {
    return {
        children: chips,
        insertBefore(item, reference) {
            this.children = this.children.filter(child => child !== item);
            const index = reference ? this.children.indexOf(reference) : this.children.length;
            this.children.splice(index, 0, item);
        }
    };
}

// Initializes the module with a Sortable stub; returns the drag options it registered and the .NET calls it made.
async function initializeEditor({ invokeResult = () => Promise.resolve() } = {}) {
    const sortable = { options: null, destroyed: false, destroy() { this.destroyed = true; } };
    globalThis.window = { Sortable: { create: (container, options) => { sortable.options = options; return sortable; } } };
    const calls = [];
    const dotNet = { invokeMethodAsync: (...args) => { calls.push(args); return invokeResult(); } };
    const { initialize } = await import(pathToFileURL(modulePath).href);
    const handle = initialize({}, dotNet, 'OnFieldReordered');
    return { sortable, calls, handle };
}

test('DisplayFieldOrder: a drop restores the DOM and reports the field and its new index to .NET', async () => {
    const { sortable, calls } = await initializeEditor();
    const [artist, title, requester] = ['artist', 'title', 'requester'].map(createChip);
    // Sortable has already moved artist to the end.
    const list = createList(title, requester, artist);

    sortable.options.onUpdate({ oldDraggableIndex: 0, newDraggableIndex: 2, item: artist, from: list });

    assert.deepEqual(list.children, [artist, title, requester]);
    assert.deepEqual(calls, [['OnFieldReordered', 'artist', 2]]);
});

test('DisplayFieldOrder: a drop back at the same index or without a field name is ignored', async () => {
    const { sortable, calls } = await initializeEditor();
    const artist = createChip('artist');
    const unnamed = createChip('');
    const list = createList(artist, unnamed);

    sortable.options.onUpdate({ oldDraggableIndex: 0, newDraggableIndex: 0, item: artist, from: list });
    sortable.options.onUpdate({ oldDraggableIndex: 1, newDraggableIndex: 0, item: unnamed, from: list });

    assert.deepEqual(calls, []);
    assert.deepEqual(list.children, [artist, unnamed]);
});

test('DisplayFieldOrder: a failed .NET call is reported while open and silent after dispose', async t => {
    const error = t.mock.method(console, 'error', () => { });
    const { sortable, handle } = await initializeEditor({ invokeResult: () => Promise.reject(new Error('page closed')) });
    const drop = () => {
        const artist = createChip('artist');
        sortable.options.onUpdate({ oldDraggableIndex: 0, newDraggableIndex: 1, item: artist, from: createList(createChip('title'), artist) });
        return new Promise(resolve => setImmediate(resolve));
    };

    await drop();
    const reportedWhileOpen = error.mock.callCount();
    handle.dispose();
    await drop();

    assert.equal(reportedWhileOpen, 1);
    assert.equal(sortable.destroyed, true);
    assert.equal(error.mock.callCount(), 1);
});

test('DisplayFieldOrder: without SortableJS, initialize fails so the editor offers the move buttons', async () => {
    globalThis.window = {};
    const { initialize } = await import(pathToFileURL(modulePath).href);

    assert.throws(() => initialize({}, {}, 'OnFieldReordered'), { message: 'SortableJS is not loaded.' });
});
