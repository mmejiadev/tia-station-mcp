// The dial is a half circle round (100, 100), swept clockwise from the left (Min) to the right (Max): the value's arc at
// radius 78, the bands outside it at radius 89. The colour is decided by the value against the limits themselves, so a
// limit set above Max never turns a full gauge amber or red.
var CENTRE_X = 100;
var CENTRE_Y = 100;
var BAND_RADIUS = 89;
var NORMAL = 'rgb(0, 200, 83)';
var WARN = 'rgb(255, 179, 0)';
var ALARM = 'rgb(229, 57, 53)';

function share(value, min, max) {
    if (max <= min) { return 0; }
    return Math.max(0, Math.min(1, (value - min) / (max - min)));
}

function point(fraction, radius) {
    var angle = Math.PI * (1 + fraction);
    return (CENTRE_X + radius * Math.cos(angle)).toFixed(2) + ' ' + (CENTRE_Y + radius * Math.sin(angle)).toFixed(2);
}

function arc(from, to) {
    if (to <= from) { return ''; }
    return 'M' + point(from, BAND_RADIUS) + ' A' + BAND_RADIUS + ' ' + BAND_RADIUS + ' 0 0 1 ' + point(to, BAND_RADIUS);
}

function render(p) {
    var min = TiaStation.number(p.Min);
    var max = TiaStation.number(p.Max);
    var value = TiaStation.number(p.Value);
    var warn = share(TiaStation.number(p.WarnLimit), min, max);
    var alarm = Math.max(warn, share(TiaStation.number(p.AlarmLimit), min, max));
    var at = share(value, min, max);
    var decimals = Math.max(0, Math.min(6, Math.round(TiaStation.number(p.Decimals))));
    var alarmLimit = TiaStation.number(p.AlarmLimit);
    var warnLimit = TiaStation.number(p.WarnLimit);
    var alarming = value >= alarmLimit;
    var state = alarming ? ALARM : (value >= warnLimit ? WARN : NORMAL);
    var root = document.getElementById('root');

    document.getElementById('normal').setAttribute('d', arc(0, warn));
    document.getElementById('warn').setAttribute('d', arc(warn, alarm));
    document.getElementById('alarm').setAttribute('d', arc(alarm, 1));
    document.getElementById('fill').style.strokeDasharray = (at * 100).toFixed(2) + ' 100';
    document.getElementById('needle').style.transform = 'rotate(' + (at * 180).toFixed(2) + 'deg)';

    root.style.setProperty('--state', state);
    root.classList.toggle('alarming', alarming);
    document.getElementById('value').textContent = value.toFixed(decimals);
    document.getElementById('unit').textContent = p.Unit || '';
    document.getElementById('min').textContent = String(min);
    document.getElementById('max').textContent = String(max);
    document.getElementById('label').textContent = p.Label || '';
}
