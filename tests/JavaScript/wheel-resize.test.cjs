const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const assert = require('node:assert/strict');
const { test } = require('node:test');

// The dashboard and the OBS overlay both load this one script.
const interopPath = path.resolve(__dirname, '../../src/SonglistSpinner.Desktop/wwwroot/spinner/SongSpinner.interop.js');

test('SongSpinner.interop.js: resizing preserves the spinning wheel and final rotation', () => {
    const source = fs.readFileSync(interopPath, 'utf8');
    let observer, timer;
    const wheels = [];
    class Wheel {
        constructor() { this.rotation = 0; this.resizes = 0; wheels.push(this); }
        remove() { this.removed = true; }
        spinToItem(index) { this.target = index; this.spinning = true; }
        resize() { this.resizes++; }
        draw() { }
    }
    const container = {};
    const context = {
        window: { spinWheel: { Wheel }, ResizeObserver: true, SonglistSpinnerContracts: {} },
        spinWheel: { Wheel },
        document: {
            getElementById: () => container,
            createElement: () => ({ getContext: () => ({ measureText: () => ({ width: 10 }) }) })
        },
        ResizeObserver: class { constructor(callback) { observer = callback; } observe() { } },
        setTimeout(callback) { timer = callback; },
        clearTimeout() { },
        performance: { now: () => 1000 }
    };
    vm.createContext(context);
    vm.runInContext(source, context);
    const interop = context.window.SpinnerInterop;
    interop.createWheel([{ label: 'First request' }, { label: 'Second request' }], []);
    interop.setupResizeObserver();
    interop.spinToItem(1, 5000);
    const original = wheels[0];
    original.rotation = 123;
    observer(); timer();
    assert.equal(wheels.length, 1);
    assert.equal(original.removed, undefined);
    assert.equal(original.target, 1);
    assert.equal(original.spinning, true);
    assert.equal(original.rotation, 123);
    original.spinning = false;
    original.rotation = 270;
    observer(); timer();
    assert.equal(wheels.length, 1);
    assert.equal(original.rotation, 270);
    assert.equal(original.resizes, 2);
});
