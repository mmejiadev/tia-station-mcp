var FAULT_COLOR = 'rgb(229, 57, 53)';
var STOPPED_COLOR = 'rgb(122, 131, 143)';

function setup() {
    document.getElementById('root').addEventListener('click', function () {
        TiaStation.fire('Clicked', TiaStation.bool(WebCC.Properties.Running));
    });
}

// One turn takes 1.6 s at 0 % and 0.18 s at 100 %: fast enough to read as running, slow enough to see.
function periodFor(speed) {
    var share = Math.max(0, Math.min(100, TiaStation.number(speed))) / 100;
    return (1.6 - share * 1.42).toFixed(2) + 's';
}

function render(p) {
    var running = TiaStation.bool(p.Running);
    var fault = TiaStation.bool(p.Fault);
    var root = document.getElementById('root');
    var state = fault ? FAULT_COLOR : (running ? TiaStation.color(p.RunColor) : STOPPED_COLOR);

    root.classList.toggle('running', running && !fault);
    root.classList.toggle('fault', fault);
    root.style.setProperty('--state', state);
    root.style.setProperty('--period', periodFor(p.Speed));
    document.getElementById('label').textContent = p.Label || '';
    document.getElementById('speed').textContent = fault ? 'FAULT' : (running ? (TiaStation.bool(p.ShowSpeed) ? Math.round(TiaStation.number(p.Speed)) + ' %' : 'RUN') : 'STOP');
}
