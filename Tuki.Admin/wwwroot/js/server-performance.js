(() => {
    const root = document.querySelector('[data-server-dashboard]');
    if (!root) return;

    const snapshotUrl = root.dataset.snapshotUrl;
    const refreshButton = root.querySelector('[data-refresh-server]');
    const alertBox = root.querySelector('[data-monitoring-alert]');
    let refreshTimer = null;

    const formatBytes = value => {
        if (value === null || value === undefined) return 'Unavailable';
        let bytes = Number(value);
        if (!Number.isFinite(bytes)) return 'Unavailable';
        const units = ['B', 'KB', 'MB', 'GB', 'TB'];
        let unit = 0;
        while (bytes >= 1024 && unit < units.length - 1) {
            bytes /= 1024;
            unit += 1;
        }
        return `${bytes.toFixed(bytes >= 100 || unit === 0 ? 0 : 1)} ${units[unit]}`;
    };

    const formatPeso = value => {
        const amount = Number(value);
        if (!Number.isFinite(amount)) return '₱0.0000';
        return amount < 1
            ? `₱${amount.toFixed(4)}`
            : `₱${amount.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
    };

    const formatUptime = seconds => {
        const total = Math.max(0, Number(seconds) || 0);
        const days = Math.floor(total / 86400);
        const hours = Math.floor((total % 86400) / 3600);
        const minutes = Math.floor((total % 3600) / 60);
        const secs = Math.floor(total % 60);
        return days > 0 ? `${days}d ${hours}h ${minutes}m` : `${hours}h ${minutes}m ${secs}s`;
    };

    const statusClass = status => {
        const normalized = String(status || '').toLowerCase();
        return normalized === 'healthy' ? 'is-healthy'
            : normalized === 'degraded' ? 'is-degraded'
                : normalized === 'unhealthy' ? 'is-unhealthy'
                    : 'is-unknown';
    };

    const setText = (selector, value) => {
        const element = root.querySelector(selector);
        if (element) element.textContent = value;
    };

    const updateStatusCard = snapshot => {
        const card = root.querySelector('[data-overall-card]');
        if (card) {
            card.classList.remove('is-healthy', 'is-degraded', 'is-unhealthy', 'is-unknown');
            card.classList.add(statusClass(snapshot.status));
        }
        setText('[data-overall-status]', snapshot.status || 'Unknown');
    };

    const updateServices = services => {
        if (!Array.isArray(services)) return;
        services.forEach(service => {
            const card = root.querySelector(`[data-service-key="${CSS.escape(service.key || '')}"]`);
            if (!card) return;
            card.classList.remove('is-healthy', 'is-degraded', 'is-unhealthy', 'is-unknown');
            card.classList.add(statusClass(service.status));
            const status = card.querySelector('.service-status-text');
            const latency = card.querySelector('.service-latency');
            const detail = card.querySelector('small');
            if (status) status.textContent = service.status || 'Unknown';
            if (latency) latency.textContent = service.responseTimeMs === null || service.responseTimeMs === undefined ? '—' : `${Math.round(service.responseTimeMs)} ms`;
            if (detail) detail.textContent = service.detail || '';
        });
    };

    const updateAiEconomics = economics => {
        const available = economics?.persistentStorageAvailable === true;
        setText(
            '[data-ai-storage-note]',
            available
                ? `Persistent SQL history · ${economics.timeZone || 'Asia/Manila'} calendar windows`
                : 'Live process counters available · apply the AI usage migration for persistent history');

        const windows = {
            today: economics?.today,
            last7Days: economics?.last7Days,
            lifetime: economics?.lifetime
        };

        Object.entries(windows).forEach(([key, windowData]) => {
            const row = root.querySelector(`[data-ai-window="${key}"]`);
            if (!row) return;
            const write = (selector, value) => {
                const element = row.querySelector(selector);
                if (element) element.textContent = value;
            };

            if (!available || !windowData) {
                write('[data-window-trips]', '—');
                write('[data-window-calls]', '—');
                write('[data-window-input]', '—');
                write('[data-window-output]', '—');
                write('[data-window-cost]', '—');
                write('[data-window-cost-per-trip]', '—');
                return;
            }

            write('[data-window-trips]', Number(windowData.trips || 0).toLocaleString());
            write('[data-window-calls]', Number(windowData.totalCalls || 0).toLocaleString());
            write('[data-window-input]', Number(windowData.inputTokens || 0).toLocaleString());
            write('[data-window-output]', Number(windowData.outputTokens || 0).toLocaleString());
            write('[data-window-cost]', formatPeso(windowData.estimatedCostPhp || 0));
            write(
                '[data-window-cost-per-trip]',
                windowData.estimatedCostPhpPerTrip === null || windowData.estimatedCostPhpPerTrip === undefined
                    ? '—'
                    : formatPeso(windowData.estimatedCostPhpPerTrip));
        });
    };

    const updateRecentRequests = requests => {
        const tbody = root.querySelector('[data-recent-requests]');
        if (!tbody) return;
        tbody.innerHTML = '';
        if (!Array.isArray(requests) || requests.length === 0) {
            const row = document.createElement('tr');
            row.innerHTML = '<td colspan="4">No request activity recorded yet.</td>';
            tbody.appendChild(row);
            return;
        }

        requests.forEach(request => {
            const row = document.createElement('tr');
            const occurred = request.occurredAtUtc ? new Date(request.occurredAtUtc) : null;
            const status = Number(request.statusCode) || 0;
            const statusClassName = status >= 500 ? 'status-bad' : status >= 400 ? 'status-warn' : 'status-ok';
            row.innerHTML = `
                <td>${occurred && !Number.isNaN(occurred.getTime()) ? occurred.toLocaleTimeString() : '—'}</td>
                <td><code></code></td>
                <td><span class="http-status ${statusClassName}">${status || '—'}</span></td>
                <td>${Math.round(Number(request.elapsedMilliseconds) || 0)} ms</td>`;
            row.querySelector('code').textContent = request.path || '';
            tbody.appendChild(row);
        });
    };

    const drawChart = timeline => {
        const canvas = document.getElementById('requestPerformanceChart');
        if (!canvas) return;
        const empty = root.querySelector('[data-chart-empty]');
        const points = Array.isArray(timeline) ? timeline : [];
        if (points.length === 0) {
            if (empty) empty.classList.remove('d-none');
            const context = canvas.getContext('2d');
            context.clearRect(0, 0, canvas.width, canvas.height);
            return;
        }
        if (empty) empty.classList.add('d-none');

        const ratio = window.devicePixelRatio || 1;
        const width = canvas.clientWidth || 800;
        const height = 240;
        canvas.width = Math.floor(width * ratio);
        canvas.height = Math.floor(height * ratio);
        const ctx = canvas.getContext('2d');
        ctx.scale(ratio, ratio);
        ctx.clearRect(0, 0, width, height);

        const padding = { left: 44, right: 34, top: 18, bottom: 34 };
        const chartWidth = Math.max(1, width - padding.left - padding.right);
        const chartHeight = height - padding.top - padding.bottom;
        const maxRequests = Math.max(1, ...points.map(point => Number(point.requests) || 0));
        const maxLatency = Math.max(1, ...points.map(point => Number(point.averageResponseTimeMs) || 0));

        ctx.strokeStyle = '#dbe8e9';
        ctx.lineWidth = 1;
        ctx.font = '11px sans-serif';
        ctx.fillStyle = '#71858a';
        for (let i = 0; i <= 4; i += 1) {
            const y = padding.top + chartHeight * (i / 4);
            ctx.beginPath();
            ctx.moveTo(padding.left, y);
            ctx.lineTo(width - padding.right, y);
            ctx.stroke();
            const label = Math.round(maxRequests * (1 - i / 4));
            ctx.fillText(String(label), 8, y + 4);
        }

        const xFor = index => padding.left + (points.length === 1 ? chartWidth / 2 : chartWidth * index / (points.length - 1));
        const yRequests = value => padding.top + chartHeight * (1 - (Number(value) || 0) / maxRequests);
        const yLatency = value => padding.top + chartHeight * (1 - (Number(value) || 0) / maxLatency);

        ctx.strokeStyle = '#0d8b97';
        ctx.lineWidth = 2.5;
        ctx.beginPath();
        points.forEach((point, index) => {
            const x = xFor(index);
            const y = yRequests(point.requests);
            index === 0 ? ctx.moveTo(x, y) : ctx.lineTo(x, y);
        });
        ctx.stroke();

        ctx.strokeStyle = '#f48b1f';
        ctx.lineWidth = 2;
        ctx.setLineDash([6, 5]);
        ctx.beginPath();
        points.forEach((point, index) => {
            const x = xFor(index);
            const y = yLatency(point.averageResponseTimeMs);
            index === 0 ? ctx.moveTo(x, y) : ctx.lineTo(x, y);
        });
        ctx.stroke();
        ctx.setLineDash([]);

        const labelIndexes = points.length <= 6 ? points.map((_, index) => index) : [0, Math.floor((points.length - 1) / 2), points.length - 1];
        ctx.fillStyle = '#71858a';
        labelIndexes.forEach(index => {
            const date = new Date(points[index].hourUtc);
            const label = Number.isNaN(date.getTime()) ? '' : date.toLocaleTimeString([], { hour: 'numeric' });
            ctx.fillText(label, xFor(index) - 12, height - 10);
        });

        ctx.fillStyle = '#0d8b97';
        ctx.fillRect(width - 176, 8, 12, 3);
        ctx.fillStyle = '#577078';
        ctx.fillText('Requests', width - 158, 13);
        ctx.fillStyle = '#f48b1f';
        ctx.fillRect(width - 91, 8, 12, 3);
        ctx.fillStyle = '#577078';
        ctx.fillText('Latency', width - 73, 13);
    };

    const render = snapshot => {
        if (!snapshot) return;
        updateStatusCard(snapshot);
        setText('[data-last-updated]', snapshot.checkedAtUtc ? new Date(snapshot.checkedAtUtc).toLocaleTimeString() : '—');
        setText('[data-uptime]', formatUptime(snapshot.uptimeSeconds));
        setText('[data-cpu]', snapshot.resources?.cpuPercent === null || snapshot.resources?.cpuPercent === undefined ? 'Warming up' : `${Number(snapshot.resources.cpuPercent).toFixed(1)}%`);

        const memoryCurrent = snapshot.resources?.containerMemoryCurrentBytes ?? snapshot.resources?.workingSetBytes;
        setText('[data-memory]', formatBytes(memoryCurrent));
        setText('[data-memory-detail]', snapshot.resources?.containerMemoryLimitBytes ? `of ${formatBytes(snapshot.resources.containerMemoryLimitBytes)} container limit` : 'Working set / container usage');
        setText('[data-total-trips]', snapshot.totalTrips === null || snapshot.totalTrips === undefined ? 'Unavailable' : Number(snapshot.totalTrips).toLocaleString());
        setText('[data-ai-calls]', Number(snapshot.aiUsage?.totalCalls || 0).toLocaleString());
        setText('[data-ai-call-detail]', `${Number(snapshot.aiUsage?.intentCalls || 0).toLocaleString()} intent · ${Number(snapshot.aiUsage?.navigationCalls || 0).toLocaleString()} navigation`);
        setText('[data-ai-input-tokens]', Number(snapshot.aiUsage?.inputTokens || 0).toLocaleString());
        setText('[data-ai-output-tokens]', Number(snapshot.aiUsage?.outputTokens || 0).toLocaleString());
        setText('[data-ai-total-tokens]', Number(snapshot.aiUsage?.totalTokens || 0).toLocaleString());
        setText('[data-ai-model]', snapshot.aiUsage?.lastModel || snapshot.aiEconomics?.lastModel || 'No successful model call yet');
        setText('[data-ai-cost-php]', formatPeso(snapshot.aiUsage?.estimatedCostPhp || 0));
        setText('[data-ai-pricing]', `$${Number(snapshot.aiUsage?.inputUsdPerMillionTokens || 0).toFixed(2)}/M in · $${Number(snapshot.aiUsage?.outputUsdPerMillionTokens || 0).toFixed(2)}/M out · ₱${Number(snapshot.aiUsage?.usdToPhp || 0).toFixed(2)}/USD`);
        updateAiEconomics(snapshot.aiEconomics);
        setText('[data-total-requests]', Number(snapshot.requests?.totalRequests || 0).toLocaleString());
        setText('[data-average-response]', `${Math.round(Number(snapshot.requests?.averageResponseTimeMs) || 0)} ms`);
        setText('[data-server-errors]', Number(snapshot.requests?.serverErrors || 0).toLocaleString());
        setText('[data-error-rate]', `${Number(snapshot.requests?.errorRatePercent || 0).toFixed(2)}%`);
        setText('[data-runtime]', snapshot.resources?.isContainer ? 'Container' : 'Host process');
        setText('[data-container-name]', snapshot.resources?.containerName || '—');
        setText('[data-disk]', snapshot.resources?.diskUsagePercent === null || snapshot.resources?.diskUsagePercent === undefined ? 'Unavailable' : `${Number(snapshot.resources.diskUsagePercent).toFixed(1)}%`);
        setText('[data-disk-detail]', snapshot.resources?.diskTotalBytes ? `${formatBytes(snapshot.resources.diskUsedBytes)} / ${formatBytes(snapshot.resources.diskTotalBytes)}` : 'Not exposed by runtime');
        setText('[data-network-rx]', formatBytes(snapshot.resources?.networkReceivedBytes));
        setText('[data-network-tx]', formatBytes(snapshot.resources?.networkSentBytes));
        setText('[data-threads]', Number(snapshot.resources?.threadCount || 0).toLocaleString());
        setText('[data-process-id]', `PID ${snapshot.resources?.processId ?? '—'}`);
        setText('[data-environment]', snapshot.environment || '—');
        setText('[data-version]', `Version ${snapshot.version || '—'}`);
        updateServices(snapshot.services);
        updateRecentRequests(snapshot.recentRequests);
        drawChart(snapshot.requests?.timeline);
    };

    const showError = message => {
        if (!alertBox) return;
        alertBox.classList.remove('d-none');
        alertBox.innerHTML = '';
        const strong = document.createElement('strong');
        strong.textContent = 'Monitoring refresh failed.';
        const span = document.createElement('span');
        span.textContent = message || 'Please try again.';
        alertBox.append(strong, span);
    };

    const clearError = () => {
        if (!alertBox) return;
        alertBox.classList.add('d-none');
        alertBox.textContent = '';
    };

    const refresh = async () => {
        if (!snapshotUrl) return;
        refreshButton?.setAttribute('disabled', 'disabled');
        try {
            const response = await fetch(snapshotUrl, { headers: { Accept: 'application/json' }, cache: 'no-store' });
            if (!response.ok) {
                let message = `Monitoring request failed with HTTP ${response.status}.`;
                try {
                    const error = await response.json();
                    if (error?.message) message = error.message;
                } catch { }
                throw new Error(message);
            }
            const snapshot = await response.json();
            clearError();
            render(snapshot);
        } catch (error) {
            showError(error instanceof Error ? error.message : 'Monitoring refresh failed.');
        } finally {
            refreshButton?.removeAttribute('disabled');
        }
    };

    document.addEventListener('DOMContentLoaded', () => {
        if (window.tukiInitialServerSnapshot) render(window.tukiInitialServerSnapshot);
        refreshButton?.addEventListener('click', refresh);
        window.addEventListener('resize', () => {
            if (window.tukiInitialServerSnapshot) drawChart(window.tukiInitialServerSnapshot.requests?.timeline);
        });
        refreshTimer = window.setInterval(refresh, 15000);
    });

    window.addEventListener('beforeunload', () => {
        if (refreshTimer) window.clearInterval(refreshTimer);
    });
})();
