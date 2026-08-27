// Shared TUKI Admin interactions.
(() => {
    const findRouteActionSource = () => {
        const selectors = [
            '#route-geometry-form',
            'a[href*="/JeepneyRoutes/Plot"]',
            'a[href*="/JeepneyRoutes/Edit"]'
        ];

        for (const selector of selectors) {
            const node = document.querySelector(selector);
            const value = node?.action || node?.href;
            if (value) return value;
        }
        return null;
    };

    const routeActionUrl = (action) => {
        const source = findRouteActionSource();
        if (!source) return null;

        try {
            const url = new URL(source, window.location.origin);
            const replaced = url.pathname.replace(
                /\/JeepneyRoutes\/(?:Create|Edit|Plot|Valhalla|ValhallaPreview|SaveValhalla|VerifySavedGeometry|Publish)(?=\/|$)/i,
                `/JeepneyRoutes/${action}`
            );
            if (replaced === url.pathname) return null;
            url.pathname = replaced;
            return url.href;
        } catch {
            return null;
        }
    };

    const createWorkflowStep = (number, label, href, state) => {
        const node = href ? document.createElement('a') : document.createElement('span');
        node.className = `route-workflow-step ${state || ''}`.trim();
        if (href) node.href = href;
        else node.setAttribute('aria-disabled', 'true');
        if (state === 'current') node.setAttribute('aria-current', 'step');
        node.innerHTML = `<span class="route-workflow-number">${number}</span><span>${label}</span>`;
        return node;
    };

    const enhanceRouteWorkflow = () => {
        const path = window.location.pathname.toLowerCase();
        if (!path.includes('/jeepneyroutes/')) return;
        if (path.includes('/valhalla')) return; // Legacy advanced page: no longer part of the primary flow.
        if (document.querySelector('.route-workflow')) return;

        const header = document.querySelector('.review-detail-header');
        if (!header) return;

        const current = path.includes('/plot') ? 'geometry' : 'details';
        const detailsUrl = routeActionUrl('Edit');
        const geometryUrl = routeActionUrl('Plot');
        const canContinue = Boolean(detailsUrl || geometryUrl);

        const workflow = document.createElement('nav');
        workflow.className = 'route-workflow route-workflow-three';
        workflow.setAttribute('aria-label', 'Jeepney route setup workflow');
        workflow.append(
            createWorkflowStep('1', 'Route details', current === 'details' ? null : detailsUrl, current === 'details' ? 'current' : 'complete'),
            createWorkflowStep('2', 'Geometry & Valhalla verify', current === 'geometry' ? null : (canContinue ? geometryUrl : null), current === 'geometry' ? 'current' : ''),
            createWorkflowStep('3', 'Publish readiness', canContinue ? detailsUrl : null, '')
        );

        header.insertAdjacentElement('afterend', workflow);
    };

    const geometryHasUnsavedChanges = () => {
        const status = document.getElementById('route-editor-status');
        return Boolean(status?.textContent?.toLowerCase().includes('unsaved'));
    };

    const readDisplayedRoutePoints = () => {
        const rows = Array.from(document.querySelectorAll('#route-point-list .route-point-row'));
        const result = [];

        rows.forEach((row) => {
            const numberInputs = row.querySelectorAll('input[type="number"]');
            if (numberInputs.length >= 2) {
                const latitude = Number.parseFloat(numberInputs[0].value);
                const longitude = Number.parseFloat(numberInputs[1].value);
                if (Number.isFinite(latitude) && Number.isFinite(longitude)) {
                    result.push({ latitude, longitude });
                }
                return;
            }

            const text = row.querySelector('.small.text-muted')?.textContent || '';
            const match = text.match(/(-?\d+(?:\.\d+)?)\s*,\s*(-?\d+(?:\.\d+)?)/);
            if (match) {
                const latitude = Number.parseFloat(match[1]);
                const longitude = Number.parseFloat(match[2]);
                if (Number.isFinite(latitude) && Number.isFinite(longitude)) {
                    result.push({ latitude, longitude });
                }
            }
        });

        return result;
    };

    const enhanceGeometryVerification = () => {
        const mapNode = document.getElementById('jeepney-route-map');
        const geometryForm = document.getElementById('route-geometry-form');
        if (!mapNode || !geometryForm) return;
        if (document.getElementById('valhalla-verification-panel')) return;

        const verifyUrl = routeActionUrl('VerifySavedGeometry');
        if (!verifyUrl) return;

        const savedCount = readDisplayedRoutePoints().length;
        const panel = document.createElement('section');
        panel.id = 'valhalla-verification-panel';
        panel.className = 'review-panel mt-3 valhalla-verify-panel';
        panel.innerHTML = `
            <div class="panel-heading valhalla-verify-heading">
                <div>
                    <span class="card-kicker">VALHALLA VERIFICATION</span>
                    <h2>Compare saved geometry with the road-following route</h2>
                    <p>No copy-paste is needed. TUKI uses the geometry you already saved, samples it to a Valhalla-safe set of anchors, and overlays the generated road route for visual checking.</p>
                </div>
                <button id="verify-saved-geometry" type="button" class="btn btn-tuki" ${savedCount < 2 ? 'disabled' : ''}>
                    Verify saved route with Valhalla
                </button>
            </div>

            <div class="valhalla-verify-note">
                <strong>Saved geometry is the source of truth.</strong>
                <span>If you change any route point above, save the geometry first. Verification intentionally checks only what is already stored in the backend.</span>
            </div>

            <div id="valhalla-verify-status" class="review-alert d-none mt-3" role="status"></div>

            <div id="valhalla-comparison-workspace" class="valhalla-comparison-workspace d-none">
                <div>
                    <div id="valhalla-comparison-map" class="review-map valhalla-comparison-map"></div>
                    <div class="valhalla-map-legend" aria-label="Map comparison legend">
                        <span><i class="legend-line legend-saved"></i> Saved TUKI geometry</span>
                        <span><i class="legend-line legend-generated"></i> Valhalla generated route</span>
                    </div>
                </div>
                <aside class="valhalla-verify-summary">
                    <span class="card-kicker">COMPARISON SUMMARY</span>
                    <div class="verify-stat"><span>Saved route points</span><strong id="verify-saved-count">—</strong></div>
                    <div class="verify-stat"><span>Anchors sent to Valhalla</span><strong id="verify-anchor-count">—</strong></div>
                    <div class="verify-stat"><span>Valhalla route points</span><strong id="verify-generated-count">—</strong></div>
                    <div class="verify-guidance">
                        <strong>What to check</strong>
                        <span>The two lines should follow the same streets, turns, and travel direction. If Valhalla takes a different road, refine/save the geometry and verify again.</span>
                    </div>
                    <button id="verify-again" type="button" class="btn btn-outline-tuki">Run verification again</button>
                </aside>
            </div>`;

        geometryForm.closest('.review-panel')?.insertAdjacentElement('beforebegin', panel);

        const verifyButton = panel.querySelector('#verify-saved-geometry');
        const verifyAgain = panel.querySelector('#verify-again');
        const status = panel.querySelector('#valhalla-verify-status');
        const workspace = panel.querySelector('#valhalla-comparison-workspace');
        const comparisonMapNode = panel.querySelector('#valhalla-comparison-map');
        let comparisonMap = null;
        let savedLine = null;
        let generatedLine = null;
        let anchorMarkers = [];

        const setStatus = (message, isError) => {
            status.textContent = message;
            status.className = `review-alert ${isError ? 'review-alert-error' : 'review-alert-success'} mt-3`;
            status.classList.remove('d-none');
        };

        const ensureComparisonMap = () => {
            if (comparisonMap || typeof L === 'undefined' || !comparisonMapNode) return comparisonMap;
            comparisonMap = L.map(comparisonMapNode).setView([15.145, 120.588], 13);
            L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                maxZoom: 19,
                attribution: '&copy; OpenStreetMap contributors'
            }).addTo(comparisonMap);
            return comparisonMap;
        };

        const clearComparison = () => {
            if (savedLine) { savedLine.remove(); savedLine = null; }
            if (generatedLine) { generatedLine.remove(); generatedLine = null; }
            anchorMarkers.forEach((marker) => marker.remove());
            anchorMarkers = [];
        };

        const renderComparison = (payload) => {
            const preview = payload.preview ?? payload.Preview ?? {};
            const generated = preview.generatedPoints ?? preview.GeneratedPoints ?? [];
            const anchors = preview.waypoints ?? preview.Waypoints ?? [];
            const saved = readDisplayedRoutePoints();

            workspace.classList.remove('d-none');
            const map = ensureComparisonMap();
            if (!map) {
                setStatus('Valhalla generated a route, but the comparison map could not be initialized.', true);
                return;
            }

            window.requestAnimationFrame(() => map.invalidateSize());
            window.setTimeout(() => map.invalidateSize(), 80);
            clearComparison();

            if (saved.length >= 2) {
                savedLine = L.polyline(
                    saved.map((point) => [point.latitude, point.longitude]),
                    { weight: 6, opacity: .92, dashArray: '10 8', color: '#0d8b97' }
                ).addTo(map);
            }

            if (generated.length >= 2) {
                generatedLine = L.polyline(
                    generated.map((point) => [
                        Number(point.latitude ?? point.Latitude),
                        Number(point.longitude ?? point.Longitude)
                    ]),
                    { weight: 5, opacity: .9, color: '#f48b1f' }
                ).addTo(map);
            }

            anchors.forEach((point, index) => {
                const latitude = Number(point.latitude ?? point.Latitude);
                const longitude = Number(point.longitude ?? point.Longitude);
                if (!Number.isFinite(latitude) || !Number.isFinite(longitude)) return;
                const marker = L.circleMarker([latitude, longitude], {
                    radius: 4,
                    weight: 1,
                    fillOpacity: .85,
                    color: '#0a5b48',
                    fillColor: '#fabf3a'
                }).addTo(map);
                marker.bindTooltip(`Verification anchor ${index + 1}`);
                anchorMarkers.push(marker);
            });

            const layers = [savedLine, generatedLine].filter(Boolean);
            if (layers.length) {
                const group = L.featureGroup(layers);
                map.fitBounds(group.getBounds(), { padding: [32, 32] });
            }

            panel.querySelector('#verify-saved-count').textContent = String(payload.savedPointCount ?? payload.SavedPointCount ?? saved.length);
            panel.querySelector('#verify-anchor-count').textContent = String(payload.sampledWaypointCount ?? payload.SampledWaypointCount ?? anchors.length);
            panel.querySelector('#verify-generated-count').textContent = String(generated.length);

            setStatus('Valhalla comparison generated. Visually confirm that the saved TUKI route and Valhalla road route follow the same jeepney path.', false);
        };

        const runVerification = async () => {
            if (geometryHasUnsavedChanges()) {
                setStatus('Save your geometry changes first. Verification only checks the route that is already stored in the backend.', true);
                document.getElementById('save-route-geometry')?.scrollIntoView({ behavior: 'smooth', block: 'center' });
                return;
            }

            const points = readDisplayedRoutePoints();
            if (points.length < 2) {
                setStatus('Save at least two route points before verifying with Valhalla.', true);
                return;
            }

            const token = geometryForm.querySelector('input[name="__RequestVerificationToken"]')?.value;
            verifyButton.disabled = true;
            if (verifyAgain) verifyAgain.disabled = true;
            verifyButton.textContent = 'Verifying…';
            status.classList.add('d-none');

            try {
                const body = new URLSearchParams();
                if (token) body.set('__RequestVerificationToken', token);
                const response = await fetch(verifyUrl, {
                    method: 'POST',
                    credentials: 'same-origin',
                    headers: { 'Content-Type': 'application/x-www-form-urlencoded;charset=UTF-8' },
                    body
                });
                const payload = await response.json().catch(() => null);
                if (!response.ok) {
                    setStatus(payload?.error || 'Unable to verify this saved route with Valhalla.', true);
                    return;
                }
                renderComparison(payload);
            } catch (error) {
                console.error('Saved geometry Valhalla verification failed.', error);
                setStatus('Unable to reach the Valhalla verification workflow. Confirm the backend and Valhalla service are running, then try again.', true);
            } finally {
                verifyButton.disabled = false;
                if (verifyAgain) verifyAgain.disabled = false;
                verifyButton.textContent = 'Verify saved route with Valhalla';
            }
        };

        verifyButton?.addEventListener('click', runVerification);
        verifyAgain?.addEventListener('click', runVerification);
    };

    enhanceRouteWorkflow();
    enhanceGeometryVerification();
})();