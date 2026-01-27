/**
 * Buriza Extension Storage API
 * Wraps chrome.storage.local for secure vault storage
 * Only accessible to the extension itself (unlike localStorage)
 */
window.buriza = window.buriza || {};

window.buriza.storage = {
    /**
     * Get a value from chrome.storage.local
     * @param {string} key - Storage key
     * @returns {Promise<string|null>} - Stored value or null
     */
    get: async function (key) {
        return new Promise((resolve) => {
            chrome.storage.local.get([key], (result) => {
                resolve(result[key] || null);
            });
        });
    },

    /**
     * Set a value in chrome.storage.local
     * @param {string} key - Storage key
     * @param {string} value - Value to store
     * @returns {Promise<void>}
     */
    set: async function (key, value) {
        return new Promise((resolve, reject) => {
            const data = {};
            data[key] = value;
            chrome.storage.local.set(data, () => {
                if (chrome.runtime.lastError) {
                    reject(new Error(chrome.runtime.lastError.message));
                } else {
                    resolve();
                }
            });
        });
    },

    /**
     * Remove a value from chrome.storage.local
     * @param {string} key - Storage key
     * @returns {Promise<void>}
     */
    remove: async function (key) {
        return new Promise((resolve, reject) => {
            chrome.storage.local.remove([key], () => {
                if (chrome.runtime.lastError) {
                    reject(new Error(chrome.runtime.lastError.message));
                } else {
                    resolve();
                }
            });
        });
    },

    /**
     * Get all keys matching a prefix
     * @param {string} prefix - Key prefix to match
     * @returns {Promise<string[]>} - Array of matching keys
     */
    getKeys: async function (prefix) {
        return new Promise((resolve) => {
            chrome.storage.local.get(null, (items) => {
                const keys = Object.keys(items).filter(k => k.startsWith(prefix));
                resolve(keys);
            });
        });
    },

    /**
     * Clear all Buriza-related storage
     * @returns {Promise<void>}
     */
    clearAll: async function () {
        const keys = await this.getKeys('buriza_');
        return new Promise((resolve, reject) => {
            chrome.storage.local.remove(keys, () => {
                if (chrome.runtime.lastError) {
                    reject(new Error(chrome.runtime.lastError.message));
                } else {
                    resolve();
                }
            });
        });
    }
};
