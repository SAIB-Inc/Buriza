/**
 * Buriza Unified Storage API
 * Auto-detects chrome.storage.local (extension) or falls back to localStorage (web).
 * Called from C# via JSInterop.
 */
window.buriza = window.buriza || {};

window.buriza.storage = (() => {
    const useChrome = typeof chrome?.storage?.local !== 'undefined';

    const chromeGet = (key) => new Promise((resolve) =>
        chrome.storage.local.get([key], (r) => resolve(r[key] ?? null)));

    const chromeSet = (key, value) => new Promise((resolve, reject) =>
        chrome.storage.local.set({ [key]: value }, () =>
            chrome.runtime.lastError
                ? reject(new Error(chrome.runtime.lastError.message))
                : resolve()));

    const chromeRemove = (key) => new Promise((resolve, reject) =>
        chrome.storage.local.remove([key], () =>
            chrome.runtime.lastError
                ? reject(new Error(chrome.runtime.lastError.message))
                : resolve()));

    return useChrome ? {
        get: chromeGet,
        set: chromeSet,
        remove: chromeRemove
    } : {
        get: (key) => Promise.resolve(localStorage.getItem(key)),
        set: (key, value) => Promise.resolve(localStorage.setItem(key, value)),
        remove: (key) => Promise.resolve(localStorage.removeItem(key))
    };
})();
