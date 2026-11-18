const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

// A page with the real vendored wheel library and SongSpinner.interop.js, on a canvas that records its draw
// calls and a frame clock the test advances. The wheel tests use it where the behaviour is the library's and
// the interop's together (rotation, landing, redraws), which a fake wheel would only restate.
const webRoot = path.resolve(__dirname, '../../src/SonglistSpinner.Desktop/wwwroot');

function loadWheelPage() {
    let now = 0;
    let nextFrameId = 1;
    let frames = [];
    const drawCalls = [];
    const wheels = [];
    const resizeObservers = [];
    const timers = [];

    const drawingContext = new Proxy({}, {
        get(target, name) {
            if (name in target) return target[name];
            if (name === 'measureText') return text => ({ width: String(text).length * 5 });
            return () => { drawCalls.push(name); };
        },
        set(target, name, value) {
            target[name] = value;
            return true;
        }
    });

    class Element {
        constructor() {
            this.style = {};
            this.clientWidth = 500;
            this.clientHeight = 500;
            this.children = [];
        }

        append(child) { this.children.push(child); }
        removeChild(child) { this.children = this.children.filter(other => other !== child); }
        getContext() { return drawingContext; }
        addEventListener() { }
        removeEventListener() { }
        setAttribute() { }
        getBoundingClientRect() { return { left: 0, top: 0, width: this.clientWidth, height: this.clientHeight }; }
    }

    const container = new Element();
    const context = {
        Element,
        HTMLImageElement: class { },
        Path2D: class { moveTo() { } arc() { } },
        devicePixelRatio: 1,
        document: {
            getElementById: id => id === 'wheelContainer' ? container : null,
            createElement: () => new Element()
        },
        requestAnimationFrame(callback) {
            const id = nextFrameId++;
            frames.push({ id, callback });
            return id;
        },
        cancelAnimationFrame(id) { frames = frames.filter(frame => frame.id !== id); },
        ResizeObserver: class {
            constructor(callback) { this.callback = callback; resizeObservers.push(this); }
            observe() { }
            unobserve() { }
            disconnect() { }
        },
        setTimeout(callback) { timers.push(callback); return timers.length; },
        clearTimeout() { },
        addEventListener() { },
        removeEventListener() { },
        matchMedia: () => ({ matches: false, addEventListener() { }, removeEventListener() { } }),
        performance: { now: () => now },
        SonglistSpinnerContracts: {}
    };
    context.window = context;
    vm.createContext(context);
    vm.runInContext(fs.readFileSync(path.join(webRoot, 'spinner/spin-wheel-iife.js'), 'utf8'), context);

    const RealWheel = context.spinWheel.Wheel;
    context.spinWheel = {
        Wheel: class extends RealWheel {
            constructor(...args) { super(...args); wheels.push(this); }
        }
    };
    vm.runInContext(fs.readFileSync(path.join(webRoot, 'spinner/SongSpinner.interop.js'), 'utf8'), context);

    return {
        interop: context.SpinnerInterop,
        wheels,
        currentWheel: () => wheels.at(-1),
        // Resizes the wheel's container as the browser reports it: every observer hears of it, and the
        // interop's debounce timer then runs.
        resizeContainer(size) {
            container.clientWidth = size;
            container.clientHeight = size;
            for (const observer of resizeObservers) observer.callback();
            for (const timer of timers.splice(0)) timer();
        },
        pendingFrames: () => frames.length,
        labelDraws: () => drawCalls.filter(name => name === 'fillText').length,
        setTime(time) { now = time; },
        // Produces one frame at the given time: every callback waiting for it runs once.
        frame(time) {
            now = time;
            const due = frames;
            frames = [];
            for (const { callback } of due) callback(time);
        }
    };
}

// The clockwise turn, in degrees, the page has applied to the wheel's canvas; 0 when it is not turned.
function canvasTurn(wheel) {
    const transform = wheel.canvas.style.transform;
    if (!transform) return 0;
    const match = /^rotate\((-?[\d.]+(?:e-?\d+)?)deg\)$/.exec(transform);
    if (!match) throw new Error(`Unexpected canvas transform '${transform}'.`);
    return Number(match[1]);
}

// The item the pointer is on as the wheel shows: where the wheel is drawn plus the turn on its canvas. The
// library knows only where it drew the wheel, so this asks a second wheel drawn at the angle shown.
function itemUnderPointer(wheel) {
    const reference = loadWheelPage();
    reference.interop.createWheel(wheel.items.map(item => ({ label: item.label })), []);
    reference.frame(0);
    reference.currentWheel().rotation = wheel.rotation + canvasTurn(wheel);
    return reference.currentWheel().getCurrentIndex();
}

module.exports = { loadWheelPage, canvasTurn, itemUnderPointer };
