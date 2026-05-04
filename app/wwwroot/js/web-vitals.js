// Minimal first-party Web Vitals RUM wrapper. It uses browser PerformanceObserver
// APIs directly to avoid adding a package to the Blazor WASM bundle. Payloads
// are intentionally anonymous: path only, never query string or fragment.
window.lfmStartWebVitals = function (options) {
    const cfg = options || {};
    if (!cfg.enabled || !cfg.endpoint) return;

    const sampleRate = Math.max(0, Math.min(1, Number(cfg.sampleRate || 0)));
    if (sampleRate <= 0 || Math.random() > sampleRate) return;

    const metricIds = {};
    const reported = new Set();
    let latestLcp = null;
    let cumulativeCls = 0;
    let maxInp = 0;
    let fcpReported = false;
    let ttfbReported = false;

    function metricId(name) {
        if (!metricIds[name]) {
            metricIds[name] = `${name}-${Date.now()}-${Math.random().toString(16).slice(2)}`;
        }
        return metricIds[name];
    }

    function navigationType() {
        const nav = performance.getEntriesByType("navigation")[0];
        return (nav && nav.type) || "navigate";
    }

    function pathOnly() {
        return window.location.pathname || "/";
    }

    function connectionType() {
        const connection = navigator.connection || navigator.mozConnection || navigator.webkitConnection;
        return (connection && connection.effectiveType) || "unknown";
    }

    function payload(name, value) {
        return {
            name,
            value,
            id: metricId(name),
            navigationType: navigationType(),
            path: pathOnly(),
            viewport: {
                width: window.innerWidth || document.documentElement.clientWidth || null,
                height: window.innerHeight || document.documentElement.clientHeight || null
            },
            effectiveConnectionType: connectionType(),
            timestamp: new Date().toISOString()
        };
    }

    function post(name, value, once) {
        if (!Number.isFinite(value) || value < 0) return;
        if (once && reported.has(name)) return;
        if (once) reported.add(name);

        const body = JSON.stringify(payload(name, value));
        const blob = new Blob([body], { type: "application/json" });
        if (navigator.sendBeacon && navigator.sendBeacon(cfg.endpoint, blob)) return;

        fetch(cfg.endpoint, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body,
            keepalive: true,
            credentials: "omit"
        }).catch(() => {});
    }

    function observe(type, callback) {
        if (!("PerformanceObserver" in window)) return;
        try {
            const observer = new PerformanceObserver((list) => {
                list.getEntries().forEach(callback);
            });
            observer.observe({ type, buffered: true });
        } catch {
            // Unsupported metric type in this browser; skip it.
        }
    }

    observe("largest-contentful-paint", (entry) => {
        latestLcp = entry.startTime;
    });

    observe("layout-shift", (entry) => {
        if (!entry.hadRecentInput) cumulativeCls += entry.value || 0;
    });

    observe("event", (entry) => {
        if (entry.interactionId && entry.duration > maxInp) {
            maxInp = entry.duration;
        }
    });

    observe("paint", (entry) => {
        if (entry.name === "first-contentful-paint") {
            fcpReported = true;
            post("fcp", entry.startTime, true);
        }
    });

    function reportTtfb() {
        if (ttfbReported) return;
        const nav = performance.getEntriesByType("navigation")[0];
        if (!nav) return;
        ttfbReported = true;
        post("ttfb", Math.max(0, nav.responseStart - nav.requestStart), true);
    }

    function flushFinalMetrics() {
        if (latestLcp !== null) post("lcp", latestLcp, true);
        post("cls", cumulativeCls, true);
        if (maxInp > 0) post("inp", maxInp, true);
        if (!fcpReported) {
            const fcp = performance.getEntriesByName("first-contentful-paint")[0];
            if (fcp) post("fcp", fcp.startTime, true);
        }
        reportTtfb();
    }

    reportTtfb();
    document.addEventListener("visibilitychange", () => {
        if (document.visibilityState === "hidden") flushFinalMetrics();
    });
    window.addEventListener("pagehide", flushFinalMetrics);
};
