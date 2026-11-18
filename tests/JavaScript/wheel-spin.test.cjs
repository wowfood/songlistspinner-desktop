const assert = require('node:assert/strict');
const { test } = require('node:test');
const { loadWheelPage, canvasTurn } = require('./wheel-page.cjs');

// How SongSpinner.interop.js spins the wheel: the wheel is drawn once and its canvas is turned, because the
// library's own spin redraws every label at a new angle each frame and a long queue cannot keep up with that.
const songs = count => Array.from({ length: count }, (_, index) => ({ label: `Song ${index + 1}` }));

function closeTo(actual, expected) {
    assert.ok(Math.abs(actual - expected) < 1e-6, `expected ${expected}, got ${actual}`);
}

// Three equal slices put the middle of the second 180 degrees from the pointer, so a spin to it turns the
// wheel a full revolution and a half: 540 degrees.
function loadSpinningPage() {
    const page = loadWheelPage();
    page.interop.createWheel(songs(3), ['#ffffff']);
    page.frame(0);
    page.setTime(1000);
    page.interop.spinToItem(1, 5000);
    return page;
}

test('SongSpinner.interop.js: a spin turns the drawn wheel without redrawing its labels', () => {
    const page = loadSpinningPage();
    const wheel = page.currentWheel();
    const labelDrawsBeforeSpin = page.labelDraws();

    page.frame(1000);
    const turnAtStart = canvasTurn(wheel);
    page.frame(3500);
    const turnHalfway = canvasTurn(wheel);
    page.frame(5900);

    assert.equal(turnAtStart, 0);
    closeTo(turnHalfway, 540 * Math.sin(Math.PI / 4));
    closeTo(canvasTurn(wheel), 540 * Math.sin(0.98 * Math.PI / 2));
    assert.equal(page.labelDraws(), labelDrawsBeforeSpin);
});

test('SongSpinner.interop.js: a finished spin leaves the wheel drawn on the winner and no longer turned', () => {
    const page = loadSpinningPage();
    const wheel = page.currentWheel();
    page.frame(3500);
    const labelDrawsWhileSpinning = page.labelDraws();

    page.frame(6000);
    const turnAsItStops = canvasTurn(wheel);
    page.frame(6016);

    // The frame that ends the spin still shows the turned drawing; the next shows the wheel drawn there.
    closeTo(turnAsItStops, 540);
    assert.equal(canvasTurn(wheel), 0);
    assert.equal(wheel.getCurrentIndex(), 1);
    closeTo(wheel.rotation, 540);
    assert.ok(page.labelDraws() > labelDrawsWhileSpinning, 'the wheel is redrawn where it stopped');
});

test('SongSpinner.interop.js: a finished spin draws the wheel once where it stops', () => {
    const page = loadSpinningPage();
    page.frame(3500);
    const labelDrawsWhileSpinning = page.labelDraws();

    page.frame(6000);
    page.frame(6016);
    page.frame(6032);

    assert.equal(page.labelDraws() - labelDrawsWhileSpinning, 3);
    assert.equal(page.pendingFrames(), 0);
});

test('SongSpinner.interop.js: a spin started as the wheel stops carries on from the winner', () => {
    const page = loadSpinningPage();
    const wheel = page.currentWheel();
    page.frame(6000);

    page.interop.spinToItem(2, 5000);
    page.frame(6016);
    const turnAtStart = canvasTurn(wheel);
    page.frame(11000);
    page.frame(11016);

    // The wheel is by now drawn on the second slice, so the new spin's turn starts from nothing.
    assert.ok(turnAtStart >= 0 && turnAtStart < 5, `the new spin starts from the winner, got ${turnAtStart}`);
    assert.equal(canvasTurn(wheel), 0);
    assert.equal(wheel.getCurrentIndex(), 2);
    closeTo(wheel.rotation, 1140);
});

test('SongSpinner.interop.js: successive spins land where the wheel library would land them', () => {
    const winners = [3, 0, 6, 6, 2];
    const page = loadWheelPage();
    page.interop.createWheel(songs(7), ['#ffffff']);
    page.frame(0);
    const reference = loadWheelPage();
    reference.interop.createWheel(songs(7), ['#ffffff']);
    reference.frame(0);

    winners.forEach((winner, spin) => {
        const start = 10000 * (spin + 1);
        page.setTime(start);
        page.interop.spinToItem(winner, 5000);
        page.frame(start + 2500);
        page.frame(start + 5000);
        reference.setTime(start);
        reference.currentWheel().spinToItem(winner, 0);
        reference.frame(start);

        closeTo(page.currentWheel().rotation, reference.currentWheel().rotation);
        assert.equal(page.currentWheel().getCurrentIndex(), winner);
    });
});

test('SongSpinner.interop.js: a wheel rebuilt during a spin starts unturned and the old spin stops', () => {
    const page = loadSpinningPage();
    page.frame(3500);

    page.interop.createWheel(songs(4), ['#ffffff']);
    page.frame(3600);
    page.frame(6000);

    assert.equal(page.wheels.length, 2);
    assert.equal(canvasTurn(page.currentWheel()), 0);
    assert.equal(page.currentWheel().rotation, 0);
    assert.equal(page.pendingFrames(), 0);
});

test('SongSpinner.interop.js: a spin with no duration lands at once, and the next spin starts from there', () => {
    const page = loadWheelPage();
    page.interop.createWheel(songs(3), ['#ffffff']);
    page.frame(0);
    const wheel = page.currentWheel();
    page.setTime(1000);

    page.interop.spinToItem(1, 0);
    page.interop.spinToItem(2, 5000);
    page.frame(1000);
    const turnAtStart = canvasTurn(wheel);
    page.frame(6000);
    page.frame(6016);

    // The first spin's 540 degrees, then 240 more to the third slice and a revolution.
    closeTo(turnAtStart, 540);
    assert.equal(canvasTurn(wheel), 0);
    assert.equal(wheel.getCurrentIndex(), 2);
    closeTo(wheel.rotation, 1140);
});

test('SongSpinner.interop.js: a spin started during another carries on from where the wheel is', () => {
    const page = loadSpinningPage();
    const wheel = page.currentWheel();
    page.frame(3500);
    const turnBeforeSecondSpin = canvasTurn(wheel);

    page.setTime(3500);
    page.interop.spinToItem(2, 5000);
    page.frame(3500);
    const turnAfterSecondSpin = canvasTurn(wheel);
    page.frame(8500);
    page.frame(8516);

    assert.ok(turnBeforeSecondSpin > 0, 'the first spin has turned the wheel');
    closeTo(turnAfterSecondSpin, turnBeforeSecondSpin);
    assert.equal(canvasTurn(wheel), 0);
    assert.equal(wheel.getCurrentIndex(), 2);
    assert.ok(wheel.rotation > turnBeforeSecondSpin + 360, 'the second spin makes a full revolution of its own');
});
