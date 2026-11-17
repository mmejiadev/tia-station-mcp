var held = false;

function enabled() {
    return TiaStation.bool(WebCC.Properties.Enabled);
}

function press(event) {
    if (!enabled() || held) { return; }
    held = true;
    document.getElementById('button').classList.add('pressed');
    TiaStation.fire('Pressed');
    if (event && event.preventDefault) { event.preventDefault(); }
}

// Released is fired once for every Pressed, also when the pointer slides off, so a Bool set on Pressed is always reset.
function release() {
    if (!held) { return; }
    held = false;
    document.getElementById('button').classList.remove('pressed');
    TiaStation.fire('Released');
}

function setup() {
    var button = document.getElementById('button');
    button.addEventListener('pointerdown', press);
    button.addEventListener('pointerup', release);
    button.addEventListener('pointerleave', release);
    button.addEventListener('pointercancel', release);
}

function render(p) {
    var root = document.getElementById('root');

    root.style.setProperty('--accent', TiaStation.color(p.Color));
    root.classList.toggle('active', TiaStation.bool(p.Active));
    root.classList.toggle('disabled', !TiaStation.bool(p.Enabled));
    document.getElementById('label').textContent = p.Label || '';
    if (!TiaStation.bool(p.Enabled)) { release(); }
}
