// Named JS interop helpers invoked from Blazor WASM via IJSRuntime.
// Defined as functions on `window` so they are addressable by name from
// .NET without passing JavaScript source strings to `eval`, which is
// blocked by the site CSP (`script-src 'self' 'wasm-unsafe-eval'`).
window.lfmSetDocumentLang = function (lang) {
    const rtlPrimaries = ["ar", "he", "fa", "ur", "yi", "ji", "iw", "ps"];
    const primary = (lang || "en").toLowerCase().split(/[-_]/)[0];
    document.documentElement.lang = lang;
    document.documentElement.dir = rtlPrimaries.includes(primary) ? "rtl" : "ltr";
};

window.lfmGetBrowserLanguage = function () {
    return navigator.language || navigator.userLanguage || 'en';
};

window.lfmGetPrefersColorScheme = function () {
    try {
        return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
    } catch {
        return 'light';
    }
};

window.lfmGetStoredTheme = function () {
    try {
        return window.localStorage.getItem('lfm-theme') || null;
    } catch {
        return null;
    }
};

window.lfmRegisterAuthResumeProbe = function (id, dotNetRef) {
    window.__lfmAuthResumeProbes = window.__lfmAuthResumeProbes || new Map();
    window.lfmUnregisterAuthResumeProbe(id);

    let pending = false;
    const trigger = function () {
        if (pending) {
            return;
        }

        pending = true;
        Promise.resolve(dotNetRef.invokeMethodAsync('CheckSessionAsync'))
            .catch(function () { })
            .finally(function () {
                pending = false;
            });
    };
    const triggerWhenVisible = function () {
        if (document.visibilityState === 'visible') {
            trigger();
        }
    };

    window.addEventListener('focus', trigger);
    window.addEventListener('pageshow', trigger);
    window.addEventListener('online', trigger);
    document.addEventListener('visibilitychange', triggerWhenVisible);

    window.__lfmAuthResumeProbes.set(id, {
        trigger,
        triggerWhenVisible
    });
};

window.lfmUnregisterAuthResumeProbe = function (id) {
    const probes = window.__lfmAuthResumeProbes;
    if (!probes || !probes.has(id)) {
        return;
    }

    const registration = probes.get(id);
    window.removeEventListener('focus', registration.trigger);
    window.removeEventListener('pageshow', registration.trigger);
    window.removeEventListener('online', registration.trigger);
    document.removeEventListener('visibilitychange', registration.triggerWhenVisible);
    probes.delete(id);
};
