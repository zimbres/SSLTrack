window.sslTrack.resizeJobsFrame = function (frame) {
    // Hangfire is same-origin. Reconnect observers after each dashboard navigation.
    frame.jobsResizeObserver?.disconnect();
    frame.jobsMutationObserver?.disconnect();
    const doc = frame.contentDocument;
    if (!doc?.body) return;

    doc.documentElement.style.overflow = 'hidden';
    doc.body.style.overflow = 'hidden';
    const resize = () => {
        if (!frame.isConnected) {
            frame.jobsResizeObserver.disconnect();
            frame.jobsMutationObserver.disconnect();
            return;
        }
        const height = Math.ceil(Math.max(doc.body.scrollHeight, doc.body.getBoundingClientRect().height));
        const next = Math.max(640, height) + 'px';
        if (frame.style.height !== next) frame.style.height = next;
    };
    frame.jobsResizeObserver = new ResizeObserver(resize);
    frame.jobsResizeObserver.observe(doc.body);
    frame.jobsMutationObserver = new MutationObserver(resize);
    frame.jobsMutationObserver.observe(doc.body, { childList: true, subtree: true, characterData: true });
    resize();
};
