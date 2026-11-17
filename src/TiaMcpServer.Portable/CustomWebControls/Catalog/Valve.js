function setup() {
    document.getElementById('root').addEventListener('click', function () {
        TiaStation.fire('Clicked', TiaStation.bool(WebCC.Properties.Open));
    });
}

function render(p) {
    var open = TiaStation.bool(p.Open);
    var root = document.getElementById('root');

    root.classList.toggle('open', open);
    root.classList.toggle('fault', TiaStation.bool(p.Fault));
    root.style.setProperty('--state', TiaStation.color(open ? p.OpenColor : p.ClosedColor));
    document.getElementById('label').textContent = p.Label || '';
}
