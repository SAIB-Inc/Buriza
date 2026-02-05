export {};

interface DotNetObjectReference {
    invokeMethodAsync(methodName: string, ...args: unknown[]): Promise<unknown>;
}

declare global {
    interface Window {
        getScreenWidth(): number;
        createAreaChart(
            canvasId: string,
            data: number[],
            color: string,
            gradientStart: string,
            gradientEnd: string
        ): void;
        createDualAreaChart(
            canvasId: string,
            data1: number[],
            color1: string,
            gradientStart1: string,
            gradientEnd1: string,
            data2: number[],
            color2: string,
            gradientStart2: string,
            gradientEnd2: string
        ): void;
        destroyChart(canvasId: string): void;
        attachWindowResizeEvent(dotNetRef: DotNetObjectReference): void;
        detachWindowResizeEvent(dotNetRef: DotNetObjectReference): void;
    }
}