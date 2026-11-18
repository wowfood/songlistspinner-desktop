const assert = require('node:assert/strict');
const { test } = require('node:test');
const { loadWheelPage, canvasTurn, itemUnderPointer } = require('./wheel-page.cjs');

// The dashboard and the OBS overlay both load SongSpinner.interop.js, which redraws the wheel when its
// container changes size.
test('SongSpinner.interop.js: resizing preserves the spinning wheel and final rotation', () => {
    const page = loadWheelPage();
    page.interop.createWheel([{ label: 'First request' }, { label: 'Second request' }], []);
    page.frame(0);
    page.interop.setupResizeObserver();
    const wheel = page.currentWheel();
    page.setTime(1000);
    page.interop.spinToItem(1, 5000);
    page.frame(3500);
    const turnBeforeResize = canvasTurn(wheel);

    page.resizeContainer(300);
    page.frame(4000);
    const turnAfterResize = canvasTurn(wheel);
    page.frame(6000);
    const turnAtRest = canvasTurn(wheel);
    page.resizeContainer(400);
    page.frame(6500);

    assert.equal(page.wheels.length, 1);
    assert.ok(turnAfterResize > turnBeforeResize, 'the spin carries on after the resize');
    assert.equal(wheel.canvas.width, 400);
    assert.equal(canvasTurn(wheel), turnAtRest);
    assert.equal(itemUnderPointer(wheel), 1);
});
