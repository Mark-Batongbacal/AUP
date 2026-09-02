(() => {
    const PAGE_SIZE = 10;

    document.querySelectorAll('[data-paginated-list]').forEach(root => {
        const searchInput = root.querySelector('[data-list-search]');
        const rows = Array.from(root.querySelectorAll('tbody tr[data-search-text]'));
        const pagination = root.querySelector('[data-list-pagination]');
        const summary = root.querySelector('[data-list-summary]');
        const emptyRow = root.querySelector('[data-list-empty]');
        let page = 1;

        const normalize = value => (value || '').toLocaleLowerCase().trim();

        function filteredRows() {
            const term = normalize(searchInput?.value);
            if (!term) return rows;
            return rows.filter(row => normalize(row.dataset.searchText).includes(term));
        }

        function renderPagination(totalPages) {
            if (!pagination) return;
            pagination.innerHTML = '';
            if (totalPages <= 1) return;

            const makeButton = (label, targetPage, disabled = false, active = false) => {
                const button = document.createElement('button');
                button.type = 'button';
                button.className = active ? 'btn btn-tuki btn-sm' : 'btn btn-outline-secondary btn-sm';
                button.textContent = label;
                button.disabled = disabled;
                button.addEventListener('click', () => {
                    page = targetPage;
                    render();
                });
                return button;
            };

            pagination.appendChild(makeButton('Previous', Math.max(1, page - 1), page === 1));

            const start = Math.max(1, page - 2);
            const end = Math.min(totalPages, start + 4);
            for (let index = start; index <= end; index += 1) {
                pagination.appendChild(makeButton(String(index), index, false, index === page));
            }

            pagination.appendChild(makeButton('Next', Math.min(totalPages, page + 1), page === totalPages));
        }

        function render() {
            const matches = filteredRows();
            const totalPages = Math.max(1, Math.ceil(matches.length / PAGE_SIZE));
            if (page > totalPages) page = totalPages;

            const visible = new Set(matches.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE));
            rows.forEach(row => {
                row.hidden = !visible.has(row);
            });

            if (emptyRow) emptyRow.hidden = matches.length !== 0;
            if (summary) {
                if (matches.length === 0) {
                    summary.textContent = '0 results';
                } else {
                    const from = (page - 1) * PAGE_SIZE + 1;
                    const to = Math.min(page * PAGE_SIZE, matches.length);
                    summary.textContent = `${from}-${to} of ${matches.length}`;
                }
            }

            renderPagination(totalPages);
        }

        searchInput?.addEventListener('input', () => {
            page = 1;
            render();
        });

        render();
    });
})();
