// The inside of the tank runs from y = 14 (full) to y = 146 (empty), 132 units tall, starting at x = 16.
var INSIDE_TOP = 14;
var INSIDE_HEIGHT = 132;
var INSIDE_LEFT = 16;

function share(value, min, max) {
    if (max <= min) { return 0; }
    return Math.max(0, Math.min(1, (value - min) / (max - min)));
}

function heightOf(value, min, max) {
    return INSIDE_TOP + INSIDE_HEIGHT * (1 - share(value, min, max));
}

function render(p) {
    var level = TiaStation.number(p.Level);
    var min = TiaStation.number(p.Min);
    var max = TiaStation.number(p.Max);
    var low = TiaStation.number(p.LowLimit);
    var high = TiaStation.number(p.HighLimit);
    var decimals = Math.max(0, Math.min(6, Math.round(TiaStation.number(p.Decimals))));
    var surface = heightOf(level, min, max) - 4;

    document.getElementById('root').style.setProperty('--liquid', TiaStation.color(p.LiquidColor));
    document.getElementById('liquid').style.transform = 'translate(' + INSIDE_LEFT + 'px, ' + surface + 'px)';

    var highLine = document.getElementById('high');
    var lowLine = document.getElementById('low');
    highLine.setAttribute('y1', heightOf(high, min, max));
    highLine.setAttribute('y2', heightOf(high, min, max));
    lowLine.setAttribute('y1', heightOf(low, min, max));
    lowLine.setAttribute('y2', heightOf(low, min, max));
    highLine.classList.toggle('crossed', level > high);
    lowLine.classList.toggle('crossed', level < low);

    var value = document.getElementById('value');
    value.textContent = level.toFixed(decimals) + (p.Unit ? ' ' + p.Unit : '');
    value.classList.toggle('crossed', level > high || level < low);
    document.getElementById('label').textContent = p.Label || '';
}
