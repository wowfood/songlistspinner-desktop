const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const assert = require('node:assert/strict');
const { test } = require('node:test');

// SpinnerInterop.validateCssSettings, which blocks saving Settings with a size or colour the browser cannot use.
const webRoot = path.resolve(__dirname, '../../src/SonglistSpinner.Desktop/wwwroot');

// A browser stand-in: CSS.supports accepts plain lengths and hex or a few named colours, and a probe element
// computes to the value it was given.
function loadInterop() {
    const createElement = () => ({ style: {}, remove() { } });
    const context = {
        document: { createElement, body: { appendChild() { } } },
        CSS: {
            supports: (property, value) => property === 'color'
                ? /^(#[0-9a-f]{3,8}|red|blue|transparent)$/i.test(value)
                : /^-?\d+(\.\d+)?(px|rem|em|%)$/.test(value)
        },
        getComputedStyle: element => ({ width: element.style.width, fontSize: element.style['font-size'] })
    };
    context.window = context;
    vm.createContext(context);
    vm.runInContext(fs.readFileSync(path.join(webRoot, 'overlay/SongSpinner.contracts.js'), 'utf8'), context);
    vm.runInContext(fs.readFileSync(path.join(webRoot, 'spinner/SongSpinner.interop.js'), 'utf8'), context);
    return context.SpinnerInterop;
}

const size = (value, property = 'font-size') => ({ key: 'size', label: 'Now Playing font size', property, value });
const colors = values => ({ key: 'colors', label: 'Wheel colour', values });

test('validateCssSettings: concrete sizes and valid colour lines pass', () => {
    const interop = loadInterop();

    const errors = interop.validateCssSettings({
        sizes: [size('1.5rem'), { ...size('480px', 'width'), key: 'width' }],
        colorLists: [colors(['#ff0000', 'blue'])]
    });

    assert.deepEqual({ ...errors }, {});
});

test('validateCssSettings: a blank, keyword, variable or zero size is rejected with a keyed message', () => {
    const interop = loadInterop();
    const check = (value, property) => interop.validateCssSettings({ sizes: [size(value, property)] }).size;

    assert.equal(check('  '), 'Enter a now playing font size.');
    assert.equal(check('auto'), 'Now Playing font size must be a concrete CSS size such as 1rem or 16px.');
    assert.equal(check('var(--size)', 'width'), 'Now Playing font size must be a concrete CSS size such as 28rem or 480px.');
    assert.equal(check('0px'), 'Now Playing font size must resolve to a size greater than zero.');
});

test('validateCssSettings: an empty colour list or invalid colour lines name the lines to fix', () => {
    const interop = loadInterop();
    const check = values => interop.validateCssSettings({ colorLists: [colors(values)] }).colors;

    assert.equal(check([]), 'Enter at least one wheel colour.');
    assert.equal(check(['#ff0000', 'not-a-colour']), 'Use valid CSS colors on line 2.');
    assert.equal(check(['', 'red', 'var(--c)', 'nope', 'bad']), 'Use valid CSS colors on lines 1, 3, 4, ….');
});
