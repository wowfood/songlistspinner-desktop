const assert = require('node:assert/strict');
const { test } = require('node:test');
const { loadWheelPage, canvasTurn, itemUnderPointer } = require('./wheel-page.cjs');

// How SongSpinner.interop.js spins the wheel: the wheel is drawn once and its canvas is turned, because the
// library's own spin redraws every label at a new angle each frame and a long queue cannot keep up with that.
// The canvas stays turned where the spin stops; the wheel is not drawn again there.
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

test('SongSpinner.interop.js: a finished spin leaves the wheel turned onto the winner', () => {
    const page = loadSpinningPage();
    const wheel = page.currentWheel();

    page.frame(6000);
    const turnAsItStops = canvasTurn(wheel);
    page.frame(6016);
    page.frame(9000);

    closeTo(turnAsItStops, 540);
    closeTo(canvasTurn(wheel), 540);
    assert.equal(itemUnderPointer(wheel), 1);
});

// Drawing the wheel where it stopped and taking the turn off could not be shown in one frame: the browser
// showed the wheel at a wrong angle for a frame in between.
test('SongSpinner.interop.js: a finished spin does not draw the wheel again', () => {
    const page = loadSpinningPage();
    const wheel = page.currentWheel();
    page.frame(3500);
    const labelDrawsWhileSpinning = page.labelDraws();

    page.frame(6000);
    page.frame(6016);
    page.frame(6032);

    assert.equal(page.labelDraws(), labelDrawsWhileSpinning);
    assert.equal(wheel.rotation, 0);
    assert.equal(page.pendingFrames(), 0);
});

test('SongSpinner.interop.js: a spin started after another carries on from the winner', () => {
    const page = loadSpinningPage();
    const wheel = page.currentWheel();
    page.frame(6000);

    page.setTime(7000);
    page.interop.spinToItem(2, 5000);
    page.frame(7000);
    const turnAtStart = canvasTurn(wheel);
    page.frame(12000);

    // From the second slice it is 240 degrees to the third, and a revolution: 540 + 600.
    closeTo(turnAtStart, 540);
    closeTo(canvasTurn(wheel), 1140);
    assert.equal(itemUnderPointer(wheel), 2);
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

        closeTo(canvasTurn(page.currentWheel()), reference.currentWheel().rotation);
        assert.equal(itemUnderPointer(page.currentWheel()), winner);
    });
});

// The wheel is rebuilt whenever the queue is read again, as it is when a winner is left in the queue. A
// rebuilt wheel that started unturned would snap back from the winner to where the wheel began.
test('SongSpinner.interop.js: a wheel rebuilt after a spin stays on the winner, and its next spin carries on from there', () => {
    const page = loadSpinningPage();
    page.frame(6000);

    page.interop.createWheel(songs(3), ['#ffffff']);
    page.frame(6100);
    const wheel = page.currentWheel();
    const turnWhenBuilt = canvasTurn(wheel);
    const itemWhenBuilt = itemUnderPointer(wheel);
    page.setTime(7000);
    page.interop.spinToItem(2, 5000);
    page.frame(7000);
    const turnAtStart = canvasTurn(wheel);
    page.frame(12000);

    assert.equal(page.wheels.length, 2);
    closeTo(turnWhenBuilt, 540);
    assert.equal(itemWhenBuilt, 1);
    closeTo(turnAtStart, 540);
    closeTo(canvasTurn(wheel), 1140);
    assert.equal(itemUnderPointer(wheel), 2);
});

test('SongSpinner.interop.js: a wheel rebuilt during a spin stays where the spin had turned it, and the spin stops', () => {
    const page = loadSpinningPage();
    page.frame(3500);
    const turnWhenRebuilt = canvasTurn(page.currentWheel());

    page.interop.createWheel(songs(4), ['#ffffff']);
    page.frame(3600);
    page.frame(6000);

    assert.equal(page.wheels.length, 2);
    assert.ok(turnWhenRebuilt > 0, 'the spin has turned the wheel');
    closeTo(canvasTurn(page.currentWheel()), turnWhenRebuilt);
    assert.equal(page.pendingFrames(), 0);
});

// A turned square reaches past its own box, over the Spin button below it.
test('SongSpinner.interop.js: the wheel takes no pointer input, so a turned wheel cannot cover a button', () => {
    const page = loadWheelPage();

    page.interop.createWheel(songs(3), ['#ffffff']);

    assert.equal(page.currentWheel().canvas.style.pointerEvents, 'none');
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

    // The first spin's 540 degrees, then 240 more to the third slice and a revolution.
    closeTo(turnAtStart, 540);
    closeTo(canvasTurn(wheel), 1140);
    assert.equal(itemUnderPointer(wheel), 2);
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

    assert.ok(turnBeforeSecondSpin > 0, 'the first spin has turned the wheel');
    closeTo(turnAfterSecondSpin, turnBeforeSecondSpin);
    assert.ok(canvasTurn(wheel) > turnBeforeSecondSpin + 360, 'the second spin makes a full revolution of its own');
    assert.equal(itemUnderPointer(wheel), 2);
});
