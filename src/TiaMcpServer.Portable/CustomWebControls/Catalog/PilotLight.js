function setup() {
    document.getElementById('root').addEventListener('click', function () {
        TiaStation.fire('Clicked', TiaStation.bool(WebCC.Properties.IsOn));
    });
}

function render(p) {
    var on = TiaStation.bool(p.IsOn);
    var alarm = TiaStation.bool(p.Alarm);
    var root = document.getElementById('root');

    root.classList.toggle('on', on && !alarm);
    root.classList.toggle('alarm', alarm);
    root.style.setProperty('--lamp', TiaStation.color(alarm ? p.AlarmColor : (on ? p.OnColor : p.OffColor)));
    document.getElementById('label').textContent = p.Label || '';
}
