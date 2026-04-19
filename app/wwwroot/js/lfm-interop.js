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
