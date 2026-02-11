import { Chart, registerables } from 'chart.js';

Chart.register(...registerables);

// Store chart instances to destroy them when needed
const chartInstances: Map<string, Chart> = new Map();

// Store resize handlers for cleanup
const resizeHandlers: Map<object, () => void> = new Map();

window.getScreenWidth = function(): number {
    return window.innerWidth;
};

window.createAreaChart = function(
    canvasId: string,
    data: number[],
    color: string,
    gradientStart: string,
    gradientEnd: string
): void {
    const canvas = document.getElementById(canvasId) as HTMLCanvasElement;
    if (!canvas) return;

    // Destroy existing chart if it exists
    const existingChart = chartInstances.get(canvasId);
    if (existingChart) {
        existingChart.destroy();
    }

    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    // Create gradient
    const gradient = ctx.createLinearGradient(0, 0, 0, canvas.height);
    gradient.addColorStop(0, gradientStart);
    gradient.addColorStop(1, gradientEnd);

    const chart = new Chart(ctx, {
        type: 'line',
        data: {
            labels: data.map((_, i) => i.toString()),
            datasets: [{
                data: data,
                borderColor: color,
                backgroundColor: gradient,
                fill: true,
                tension: 0.4,
                borderWidth: 2,
                pointRadius: 0,
                pointHoverRadius: 0
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: { display: false },
                tooltip: { enabled: false }
            },
            scales: {
                x: { display: false },
                y: { display: false }
            },
            interaction: {
                intersect: false,
                mode: 'index'
            }
        }
    });

    chartInstances.set(canvasId, chart);
};

window.destroyChart = function(canvasId: string): void {
    const chart = chartInstances.get(canvasId);
    if (chart) {
        chart.destroy();
        chartInstances.delete(canvasId);
    }
};

window.createDualAreaChart = function(
    canvasId: string,
    data1: number[],
    color1: string,
    gradientStart1: string,
    gradientEnd1: string,
    data2: number[],
    color2: string,
    gradientStart2: string,
    gradientEnd2: string
): void {
    const canvas = document.getElementById(canvasId) as HTMLCanvasElement;
    if (!canvas) return;

    // Destroy existing chart if it exists
    const existingChart = chartInstances.get(canvasId);
    if (existingChart) {
        existingChart.destroy();
    }

    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    // Create gradient for first line (Today)
    const gradient1 = ctx.createLinearGradient(0, 0, 0, canvas.height);
    gradient1.addColorStop(0, gradientStart1);
    gradient1.addColorStop(1, gradientEnd1);

    // Create gradient for second line (30 days ago)
    const gradient2 = ctx.createLinearGradient(0, 0, 0, canvas.height);
    gradient2.addColorStop(0, gradientStart2);
    gradient2.addColorStop(1, gradientEnd2);

    const chart = new Chart(ctx, {
        type: 'line',
        data: {
            labels: data1.map((_, i) => i.toString()),
            datasets: [
                {
                    data: data1,
                    borderColor: color1,
                    backgroundColor: gradient1,
                    fill: true,
                    tension: 0.4,
                    borderWidth: 2,
                    pointRadius: 0,
                    pointHoverRadius: 0
                },
                {
                    data: data2,
                    borderColor: color2,
                    backgroundColor: gradient2,
                    fill: true,
                    tension: 0.4,
                    borderWidth: 2,
                    pointRadius: 0,
                    pointHoverRadius: 0
                }
            ]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: { display: false },
                tooltip: { enabled: false }
            },
            scales: {
                x: { display: false },
                y: { display: false }
            },
            interaction: {
                intersect: false,
                mode: 'index'
            }
        }
    });

    chartInstances.set(canvasId, chart);
};

window.attachWindowResizeEvent = function(dotNetRef: { invokeMethodAsync: (method: string, ...args: unknown[]) => Promise<unknown> }): void {
    const handler = () => {
        dotNetRef.invokeMethodAsync('OnResize', window.innerWidth);
    };

    // Call immediately to set initial state
    handler();

    // Store the handler for later cleanup
    resizeHandlers.set(dotNetRef, handler);
    window.addEventListener('resize', handler);
};

window.detachWindowResizeEvent = function(dotNetRef: { invokeMethodAsync: (method: string, ...args: unknown[]) => Promise<unknown> }): void {
    const handler = resizeHandlers.get(dotNetRef);
    if (handler) {
        window.removeEventListener('resize', handler);
        resizeHandlers.delete(dotNetRef);
    }
};