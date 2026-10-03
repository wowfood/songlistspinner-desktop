const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const assert = require('node:assert/strict');
const { test } = require('node:test');

// How SongSpinner.interop.js sizes and fits the labels it hands to the wheel library.
const interopPath = path.resolve(__dirname, '../../src/SonglistSpinner.Desktop/wwwroot/spinner/SongSpinner.interop.js');

// Each character measures 10px, so a label fits when it has at most 19 characters.
function createWheel(items) {
    const wheels = [];
    class Wheel {
        constructor(container, options) { this.options = options; wheels.push(this); }
        remove() { }
    }
    const context = {
        spinWheel: { Wheel },
        document: {
            getElementById: () => ({}),
            createElement: () => ({ getContext: () => ({ measureText: text => ({ width: text.length * 10 }) }) })
        }
    };
    context.window = context;
    vm.createContext(context);
    vm.runInContext(fs.readFileSync(interopPath, 'utf8'), context);
    context.SpinnerInterop.createWheel(items, ['#ffffff']);
    return wheels[0].options;
}

test('SongSpinner.interop.js: wheel labels shrink as the wheel fills, within the readable range', () => {
    const labels = count => Array.from({ length: count }, (_, index) => ({ label: `Song ${index}` }));

    assert.equal(createWheel(labels(1)).itemLabelFontSizeMax, 28);
    assert.equal(createWheel(labels(8)).itemLabelFontSizeMax, 28);
    assert.equal(createWheel(labels(20)).itemLabelFontSizeMax, 27);
    assert.equal(createWheel(labels(80)).itemLabelFontSizeMax, 12);
});

test('SongSpinner.interop.js: a label too long for its slice is cut with an ellipsis', () => {
    const options = createWheel([{ label: 'Short' }, { label: 'A very long artist name - A very long title' }]);

    assert.deepEqual(options.items.map(item => item.label), ['Short', 'A very long artist…']);
});
