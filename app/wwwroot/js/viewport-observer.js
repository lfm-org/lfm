// app/wwwroot/js/viewport-observer.js
const observers = new WeakMap();

export function start(dotnetRef, cardSelector) {
    const observer = new IntersectionObserver((entries) => {
        for (const entry of entries) {
            if (!entry.isIntersecting) continue;
            const id = entry.target.getAttribute('data-char-id');
            if (id) dotnetRef.invokeMethodAsync('OnCardVisible', id);
        }
    }, { root: null, threshold: 0.1 });

    for (const el of document.querySelectorAll(cardSelector))
        observer.observe(el);

    observers.set(dotnetRef, observer);
}

export function stop(dotnetRef) {
    const observer = observers.get(dotnetRef);
    if (observer) observer.disconnect();
    observers.delete(dotnetRef);
}
