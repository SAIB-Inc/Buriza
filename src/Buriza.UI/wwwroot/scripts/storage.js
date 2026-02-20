/**
 * Buriza Unified Storage API
 * Auto-detects chrome.storage.local (extension) or falls back to localStorage (web).
 * Called from C# via JSInterop.
 */
window.buriza = window.buriza || {};

window.buriza.storage = (() => {
    const useChrome = typeof chrome?.storage?.local !== 'undefined';

    if (!useChrome) {
        return {
            get: (key) => Promise.resolve(localStorage.getItem(key)),
            set: (key, value) => {
                try {
                    localStorage.setItem(key, value);
                    return Promise.resolve();
                } catch (e) {
                    return Promise.reject(e.name === 'QuotaExceededError'
                        ? new Error(`Storage quota exceeded while saving '${key}'`)
                        : e);
                }
            },
            remove: (key) => Promise.resolve(localStorage.removeItem(key)),
            exists: (key) => Promise.resolve(localStorage.getItem(key) !== null),
            getKeys: (prefix) => Promise.resolve(
                Object.keys(localStorage).filter(k => k.startsWith(prefix))),
            clear: () => Promise.resolve(localStorage.clear())
        };
    } else {
        const store = chrome.storage.local;

        function checkError(resolve, reject) {
            chrome.runtime.lastError
                ? reject(new Error(chrome.runtime.lastError.message))
                : resolve();
        }

        return {
            get: (key) => new Promise((resolve) =>
                store.get([key], (r) => resolve(r[key] ?? null))),
            set: (key, value) => new Promise((resolve, reject) =>
                store.set({ [key]: value }, () => checkError(resolve, reject))),
            remove: (key) => new Promise((resolve, reject) =>
                store.remove([key], () => checkError(resolve, reject))),
            exists: (key) => new Promise((resolve) =>
                store.get([key], (r) => resolve(key in r))),
            getKeys: (prefix) => new Promise((resolve) =>
                store.get(null, (r) => resolve(Object.keys(r).filter(k => k.startsWith(prefix))))),
            clear: () => new Promise((resolve, reject) =>
                store.clear(() => checkError(resolve, reject)))
        };
    }
})();
