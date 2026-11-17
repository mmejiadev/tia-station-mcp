var FAULT_COLOR = 'rgb(229, 57, 53)';
var STOPPED_COLOR = 'rgb(122, 131, 143)';

function setup() {
    document.getElementById('root').addEventListener('click', function () {
        TiaStation.fire('Clicked', TiaStation.bool(WebCC.Properties.Running));
    });
}

// The belt's stripes repeat every 20 units: one repeat takes 1.2 s at 0 % and 0.12 s at 100 %.
function periodFor(speed) {
    var share = Math.max(0, Math.min(100, TiaStation.number(speed))) / 100;
    return (1.2 - share * 1.08).toFixed(2) + 's';
}

function render(p) {
    var running = TiaStation.bool(p.Running);
    var fault = TiaStation.bool(p.Fault);
    var reverse = TiaStation.bool(p.Reverse);
    var root = document.getElementById('root');

    root.classList.toggle('running', running && !fault);
    root.classList.toggle('reverse', reverse);
    root.classList.toggle('fault', fault);
    root.style.setProperty('--state', fault ? FAULT_COLOR : (running ? TiaStation.color(p.RunColor) : STOPPED_COLOR));
    root.style.setProperty('--period', periodFor(p.Speed));
    document.getElementById('label').textContent = p.Label || '';
    document.getElementById('state').textContent = fault ? 'FAULT' : (running ? (reverse ? '◀ ' : '▶ ') + Math.round(TiaStation.number(p.Speed)) + ' %' : 'STOP');
}
