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

    const chromeExists = (key) => new Promise((resolve) =>
        chrome.storage.local.get([key], (r) => resolve(key in r)));

    const chromeGetKeys = (prefix) => new Promise((resolve) =>
        chrome.storage.local.get(null, (r) =>
            resolve(Object.keys(r).filter(k => k.startsWith(prefix)))));

    const chromeClear = () => new Promise((resolve, reject) =>
        chrome.storage.local.clear(() =>
            chrome.runtime.lastError
                ? reject(new Error(chrome.runtime.lastError.message))
                : resolve()));

    return useChrome ? {
        get: chromeGet,
        set: chromeSet,
        remove: chromeRemove,
        exists: chromeExists,
        getKeys: chromeGetKeys,
        clear: chromeClear
    } : {
        get: (key) => Promise.resolve(localStorage.getItem(key)),
        set: (key, value) => {
            try {
                localStorage.setItem(key, value);
                return Promise.resolve();
            } catch (e) {
                return Promise.reject(e.name === 'QuotaExceededError'
                    ? new Error('Storage quota exceeded')
                    : e);
            }
        },
        remove: (key) => Promise.resolve(localStorage.removeItem(key)),
        exists: (key) => Promise.resolve(localStorage.getItem(key) !== null),
        getKeys: (prefix) => Promise.resolve(
            Object.keys(localStorage).filter(k => k.startsWith(prefix))),
        clear: () => Promise.resolve(localStorage.clear())
    };
})();
