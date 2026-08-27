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

    document.addEventListener('click', (event) => {
        const link = event.target.closest('a');
        if (!isValhallaLink(link)) return;
        if (event.metaKey || event.ctrlKey || event.shiftKey || event.altKey || link.target === '_blank') return;
        event.preventDefault();
        openToolModal(link.href);
    });

    document.addEventListener('keydown', (event) => {
        if (event.key !== 'Escape') return;
        const modal = document.querySelector('.tool-modal.open');
        if (modal) closeToolModal(modal);
    });
})();
