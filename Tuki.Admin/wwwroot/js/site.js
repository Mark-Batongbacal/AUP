// Shared TUKI Admin interactions.
(() => {
    const isValhallaLink = (link) => {
        if (!link || !link.href) return false;
        try {
            const url = new URL(link.href, window.location.origin);
            return url.pathname.toLowerCase().includes('/jeepneyroutes/valhalla');
        } catch {
            return false;
        }
    };

    const createToolModal = () => {
        const modal = document.createElement('div');
        modal.className = 'tool-modal';
        modal.setAttribute('aria-hidden', 'true');
        modal.innerHTML = `
            <div class="tool-modal-backdrop" data-tool-close></div>
            <section class="tool-modal-dialog" role="dialog" aria-modal="true" aria-label="Valhalla route tester">
                <header class="tool-modal-header">
                    <div>
                        <span class="card-kicker">INTEGRATED ROUTE TOOL</span>
                        <strong>Valhalla route tester</strong>
                        <small>Generate, compare, and save without leaving your current route workspace.</small>
                    </div>
                    <button type="button" class="tool-modal-close" data-tool-close aria-label="Close route tester">×</button>
                </header>
                <iframe class="tool-modal-frame" title="Valhalla route tester"></iframe>
            </section>`;
        document.body.appendChild(modal);
        modal.querySelectorAll('[data-tool-close]').forEach((button) => button.addEventListener('click', () => closeToolModal(modal)));
        return modal;
    };

    const closeToolModal = (modal) => {
        modal.classList.remove('open');
        modal.setAttribute('aria-hidden', 'true');
        document.body.classList.remove('tool-modal-open');
        window.setTimeout(() => {
            const frame = modal.querySelector('.tool-modal-frame');
            if (frame) frame.src = 'about:blank';
        }, 180);
    };

    const openToolModal = (href) => {
        const modal = document.querySelector('.tool-modal') || createToolModal();
        const frame = modal.querySelector('.tool-modal-frame');
        frame.src = href;
        modal.classList.add('open');
        modal.setAttribute('aria-hidden', 'false');
        document.body.classList.add('tool-modal-open');
    };

    const geometryHasUnsavedChanges = () => {
        const status = document.getElementById('route-editor-status');
        return Boolean(status?.textContent?.toLowerCase().includes('unsaved'));
    };

    const confirmValhallaOpen = () => {
        if (!geometryHasUnsavedChanges()) return true;
        return window.confirm(
            'You have unsaved geometry changes. Valhalla compares against the last saved route geometry, so the tester will not include these unsaved edits yet.\n\nOpen Valhalla anyway?'
        );
    };

    const findRouteActionSource = () => {
        const selectors = [
            '#route-geometry-form',
            '#valhalla-form',
            'a[href*="/JeepneyRoutes/Plot"]',
            'a[href*="/JeepneyRoutes/Valhalla"]',
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
                /\/JeepneyRoutes\/(?:Create|Edit|Plot|Valhalla|ValhallaPreview|SaveValhalla|Publish)(?=\/|$)/i,
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
        if (document.querySelector('.route-workflow')) return;

        const header = document.querySelector('.review-detail-header');
        if (!header) return;

        let current = 'details';
        if (path.includes('/plot')) current = 'geometry';
        else if (path.includes('/valhalla')) current = 'valhalla';

        const detailsUrl = routeActionUrl('Edit');
        const geometryUrl = routeActionUrl('Plot');
        const valhallaUrl = routeActionUrl('Valhalla');
        const canContinue = Boolean(detailsUrl || geometryUrl || valhallaUrl);

        const workflow = document.createElement('nav');
        workflow.className = 'route-workflow';
        workflow.setAttribute('aria-label', 'Jeepney route setup workflow');
        workflow.append(
            createWorkflowStep('1', 'Route details', current === 'details' ? null : detailsUrl, current === 'details' ? 'current' : 'complete'),
            createWorkflowStep('2', 'Geometry', current === 'geometry' ? null : (canContinue ? geometryUrl : null), current === 'geometry' ? 'current' : ''),
            createWorkflowStep('3', 'Test with Valhalla', current === 'valhalla' ? null : (canContinue ? valhallaUrl : null), current === 'valhalla' ? 'current' : ''),
            createWorkflowStep('4', 'Publish readiness', canContinue ? detailsUrl : null, '')
        );

        header.insertAdjacentElement('afterend', workflow);
    };

    const makeValhallaButton = (href, label, className) => {
        const link = document.createElement('a');
        link.href = href;
        link.className = className;
        link.innerHTML = `<span aria-hidden="true">⚡</span> ${label}`;
        return link;
    };

    const enhanceGeometryEditor = () => {
        const map = document.getElementById('jeepney-route-map');
        if (!map) return;

        const valhallaUrl = routeActionUrl('Valhalla');
        const editable = Boolean(document.getElementById('save-route-geometry'));
        if (!valhallaUrl || !editable) return;

        const header = document.querySelector('.review-detail-header');
        const headerActions = header?.querySelector(':scope > div:last-child');
        if (headerActions && !headerActions.querySelector('.route-valhalla-header')) {
            headerActions.appendChild(
                makeValhallaButton(valhallaUrl, 'Test with Valhalla', 'btn btn-tuki route-valhalla-header')
            );
        }

        const fitButton = document.getElementById('fit-route-points');
        const mapToolbar = fitButton?.parentElement;
        if (mapToolbar && !mapToolbar.querySelector('.route-valhalla-toolbar')) {
            mapToolbar.appendChild(
                makeValhallaButton(valhallaUrl, 'Test route with Valhalla', 'btn btn-outline-tuki route-valhalla-toolbar')
            );
        }

        const geometryForm = document.getElementById('route-geometry-form');
        if (geometryForm && !geometryForm.querySelector('.route-geometry-next')) {
            const next = document.createElement('div');
            next.className = 'route-geometry-next';
            next.innerHTML = `
                <div>
                    <strong>Next: verify the road-following route</strong>
                    <span>Save your geometry, then compare selected waypoint anchors with Valhalla before publishing.</span>
                </div>`;
            next.appendChild(
                makeValhallaButton(valhallaUrl, 'Open Valhalla tester', 'btn btn-outline-tuki')
            );
            geometryForm.appendChild(next);
        }
    };

    const enhanceValhallaPage = () => {
        const form = document.getElementById('valhalla-form');
        if (!form) return;
        const geometryUrl = routeActionUrl('Plot');
        if (!geometryUrl) return;

        const header = document.querySelector('.review-detail-header');
        if (!header || header.querySelector('.route-geometry-return')) return;

        const actions = header.querySelector(':scope > span:last-child')?.parentElement === header
            ? null
            : header.querySelector(':scope > div:last-child');

        const link = document.createElement('a');
        link.href = geometryUrl;
        link.className = 'btn btn-outline-tuki route-geometry-return';
        link.textContent = 'Back to geometry editor';

        if (actions && actions !== header.firstElementChild) {
            actions.appendChild(link);
        } else {
            const wrapper = document.createElement('div');
            wrapper.className = 'd-flex gap-2 flex-wrap align-items-center';
            const status = header.querySelector(':scope > .status-pill');
            if (status) wrapper.appendChild(status);
            wrapper.appendChild(link);
            header.appendChild(wrapper);
        }
    };

    document.addEventListener('click', (event) => {
        const link = event.target.closest('a');
        if (!isValhallaLink(link)) return;
        if (event.metaKey || event.ctrlKey || event.shiftKey || event.altKey || link.target === '_blank') return;
        event.preventDefault();
        if (!confirmValhallaOpen()) return;
        openToolModal(link.href);
    });

    document.addEventListener('keydown', (event) => {
        if (event.key !== 'Escape') return;
        const modal = document.querySelector('.tool-modal.open');
        if (modal) closeToolModal(modal);
    });

    enhanceRouteWorkflow();
    enhanceGeometryEditor();
    enhanceValhallaPage();
})();