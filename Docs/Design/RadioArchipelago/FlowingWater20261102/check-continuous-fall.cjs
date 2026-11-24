// Offline directional contract check. It evaluates the production shader's
// scalar carrier expression against an independent physical quarter-circle
// bank fixture; it does not replace Unity shader compilation/render proof.
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');

const root = path.resolve(__dirname, '../../../..');
const shaderPath = path.join(root, 'DancingCatsClone/Assets/RadioArchipelago/Art/CoralWater.shader');
const shader = fs.readFileSync(shaderPath, 'utf8');
const expression = shader.match(/float2 AuthoredFlowCoordinate\(float2 uv, float time\)\s*\{\s*return float2\(([^;]+)\);\s*\}/);
assert.ok(expression, 'Shader must expose its authored-UV carrier for the bank/curtain contract');
// Only arithmetic from this small production function is evaluated. No second
// copy of its motion equation lives in the test.
assert.match(expression[1], /^[\w\s.,*+\-/]+$/);
const coordinate = new Function('uv', 'time', '_CurrentSpeed', `return [${expression[1]}];`);
assert.match(shader, /currentPosition\s*=\s*AuthoredFlowCoordinate\(v\.uv,time\)/,
    'Rendered authored water must consume the checked carrier');

const radius = 3;
const arcLength = Math.PI * radius / 2;
function physicalPoint(s) {
    if (s < 0) return [s, 0];
    if (s <= arcLength) return [radius * Math.sin(s / radius), radius * (Math.cos(s / radius) - 1)];
    return [radius, -radius - (s - arcLength)];
}
function tangent(s) {
    if (s < 0) return [1, 0];
    if (s <= arcLength) return [Math.cos(s / radius), -Math.sin(s / radius)];
    return [0, -1];
}
function close(actual, expected, message) {
    assert.ok(Math.abs(actual - expected) < 1e-9, `${message}: ${actual} != ${expected}`);
}

const currentSpeed = 4.2, flowSpeed = .65;
const realTime = 7.1, elapsed = .25;
const travel = currentSpeed * flowSpeed * elapsed;
const checks = [];
for (const [name, station] of [
    ['horizontal stream', -4],
    ['previous cancellation angle 48.256301968 degrees', radius * 48.256301968393466 * Math.PI / 180],
    ['vertical curtain', arcLength + 5],
    ['crossing roll-curtain seam', arcLength - travel / 2],
]) {
    const initial = coordinate({x: .43, y: station}, realTime * flowSpeed, currentSpeed);
    const moved = coordinate({x: .43, y: station + travel}, (realTime + elapsed) * flowSpeed, currentSpeed);
    close(moved[0], initial[0], `${name} keeps cross-stream identity`);
    close(moved[1], initial[1], `${name} carries the same feature downstream`);
    const a = physicalPoint(station), b = physicalPoint(station + travel), t = tangent(station);
    const progress = (b[0] - a[0]) * t[0] + (b[1] - a[1]) * t[1];
    assert.ok(progress > .6, `${name} must move forward in physical space`);
    if (station > 0) assert.ok(b[1] < a[1], `${name} must also descend`);
    checks.push({name, downstreamMetres: travel, tangentProgressMetres: progress});
}
// Two materials meet at the same authored station and share one real clock.
// The fixture deliberately uses a nonzero world height and UV origin: neither
// may reset the carrier when the stream becomes a curtain.
const materialCheck = process.argv.includes('--materials');
function settings(name) {
    if (!materialCheck) return {currentSpeed, flowSpeed, useFlowUV: 1, clock: -1};
    const file = path.join(root, `DancingCatsClone/Assets/RadioArchipelago/Content/Islands/AmberWinds/RA_Amber_${name}.mat`);
    const text = fs.readFileSync(file, 'utf8');
    const read = key => Number(text.match(new RegExp(`- ${key}: ([^\\r\\n]+)`))?.[1]);
    return {currentSpeed: read('_CurrentSpeed'), flowSpeed: read('_FlowSpeed'), useFlowUV: read('_UseFlowUV'), clock: read('_WaterPreviewTime')};
}
const stream = settings('Stream'), curtain = settings('Waterfall');
for (const [name, actual] of [['Stream', stream], ['Waterfall', curtain]]) {
    assert.equal(actual.useFlowUV, 1, `${name} must opt in to the authored carrier`);
    assert.equal(actual.clock, -1, `${name} must use real runtime time`);
    close(actual.currentSpeed * actual.flowSpeed, 2.73, `${name} configured physical speed`);
}
const seamStation = 48.401121 + arcLength;
const streamPhase = coordinate({x: .43, y: seamStation}, realTime * stream.flowSpeed, stream.currentSpeed);
const curtainPhase = coordinate({x: .43, y: seamStation}, realTime * curtain.flowSpeed, curtain.currentSpeed);
assert.deepEqual(streamPhase, curtainPhase, 'Joined materials share carrier phase and clock');
close((seamStation - coordinate({x: 0, y: seamStation}, flowSpeed, currentSpeed)[1]), 2.73,
    'Configured world-time speed is metres per second');

// Original-coordinate-blend counterexample at the real R3 bank angle. This
// records why the change is needed; the renderer must no longer use this
// blend on UV-authored meshes. Other old foam/normal layers could still move.
const badTilt = 48.256301968393466 * Math.PI / 180;
const q = (Math.cos(badTilt) - .35) / .5;
const fall = 1 - q * q * (3 - 2 * q);
const oldFirstLayerTemporalDerivative = -(1 - fall) * .095 * currentSpeed + fall * .09 * 10;
close(oldFirstLayerTemporalDerivative, 0, 'Original first-layer cancellation reproduced');

console.log(JSON.stringify({pass: true, scope: 'Offline production-carrier/physical-bank fixture; native render proof remains separate', savedAmberMaterialsChecked: materialCheck, realMetresPerSecond: 2.73, checks, seamPhase: streamPhase, originalFirstLayerCancellation: {tiltDegrees: badTilt * 180 / Math.PI, temporalDerivative: oldFirstLayerTemporalDerivative}}, null, 2));
