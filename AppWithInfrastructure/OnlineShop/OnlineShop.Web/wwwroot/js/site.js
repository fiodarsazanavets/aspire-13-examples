var map = null;

window.initializeMap = () => {
    map = new ol.Map({
        target: 'map',
        layers: [
            new ol.layer.Tile({
                source: new ol.source.OSM()
            })
        ],
        view: new ol.View({
            center: ol.proj.fromLonLat([-0.1276, 51.5074]),
            zoom: 12
        })
    });
}

window.markerLayer = null;

window.addMarker = (lat, lon) => {
    if (window.markerLayer) {
        window.map.removeLayer(window.markerLayer);
    }

    const marker = new ol.Feature({
        geometry: new ol.geom.Point(
            ol.proj.fromLonLat([lon, lat]))
    });

    const markerStyle = new ol.style.Style({
        image: new ol.style.Circle({
            radius: 10, // Radius of the circle in pixels
            fill: new ol.style.Fill({
                color: 'rgba(255, 0, 0, 0.8)'
            }), // Fill color (red with 80% opacity)
            stroke: new ol.style.Stroke({
                color: 'black',
                width: 2
            }) // Optional stroke (black border)
        })
    });

    marker.setStyle(markerStyle);

    const vectorSource = new ol.source.Vector({
        features: [marker]
    });

    window.markerLayer = new ol.layer.Vector({
        source: vectorSource
    });

    window.map.addLayer(window.markerLayer);
};