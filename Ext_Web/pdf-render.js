// Evidencias SQA — pdf-render.js
// Offscreen document (MV3) que renderiza documentos PDF completos vía pdf.js.
// El service worker le pide renderizar un PDF (action 'renderPdf') y este
// documento entrega el PNG resultante en chunks (action 'pdfRenderChunk').

const { getDocument, GlobalWorkerOptions } = globalThis.pdfjsLib;

GlobalWorkerOptions.workerSrc = chrome.runtime.getURL('lib/pdf.worker.min.js');

const TARGET_WIDTH = 1500;          // ancho objetivo en px (A4 ≈ 2x)
const MAX_CANVAS_HEIGHT = 30000;    // límite de altura del canvas (Chrome: 32767)
const CHUNK_BASE64_LENGTH = 1500000; // ~1.5MB base64 por mensaje

function sendToSw(message) {
    try { chrome.runtime.sendMessage(message); } catch (e) {}
}

function sendProgress(tabId, progress) {
    sendToSw({ action: 'pdfRenderProgress', tabId, progress });
}

function blobToDataUrl(blob) {
    return new Promise((resolve, reject) => {
        const reader = new FileReader();
        reader.onloadend = () => resolve(reader.result);
        reader.onerror = () => reject(reader.error || new Error('No se pudo leer el PNG'));
        reader.readAsDataURL(blob);
    });
}

async function renderPdf(message) {
    const { pdfUrl, tabId } = message;

    const resp = await fetch(pdfUrl);
    if (!resp.ok) throw new Error(`HTTP ${resp.status} al obtener el PDF`);
    const buffer = await resp.arrayBuffer();

    const pdf = await getDocument({
        data: buffer,
        isEvalSupported: false,
        useSystemFonts: true
    }).promise;

    sendProgress(tabId, 15);

    const pages = [];
    let maxW = 0;
    let totalH = 0;
    for (let i = 1; i <= pdf.numPages; i++) {
        const page = await pdf.getPage(i);
        const vp = page.getViewport({ scale: 1 });
        pages.push({ page, w: vp.width, h: vp.height });
        if (vp.width > maxW) maxW = vp.width;
        totalH += vp.height;
        if (i % 5 === 0) {
            sendProgress(tabId, 15 + Math.round((i / pdf.numPages) * 35));
        }
    }

    if (pages.length === 0) throw new Error('El PDF no contiene páginas');

    let scale = Math.min(TARGET_WIDTH / maxW, MAX_CANVAS_HEIGHT / totalH);
    if (!isFinite(scale) || scale <= 0) scale = 1;
    if (scale > 3) scale = 3; // nunca ampliar más de 3x

    const W = Math.ceil(maxW * scale);
    const H = Math.ceil(totalH * scale);

    const canvas = new OffscreenCanvas(W, H);
    const ctx = canvas.getContext('2d', { alpha: false });
    ctx.fillStyle = '#ffffff';
    ctx.fillRect(0, 0, W, H);

    let y = 0;
    for (const { page, w, h } of pages) {
        const viewport = page.getViewport({ scale });
        const pageCanvas = new OffscreenCanvas(Math.ceil(w * scale), Math.ceil(h * scale));
        await page.render({ canvasContext: pageCanvas.getContext('2d', { alpha: false }), viewport }).promise;
        ctx.drawImage(pageCanvas, 0, y);
        y += Math.ceil(h * scale);
        pageCanvas.width = 0;
        pageCanvas.height = 0;
        try { page.cleanup(); } catch (e) {}
        sendProgress(tabId, 50 + Math.round((y / H) * 30));
    }

    sendProgress(tabId, 82);

    const blob = await canvas.convertToBlob({ type: 'image/png' });
    const dataUrl = await blobToDataUrl(blob);
    const base64 = dataUrl.split(',')[1];
    const total = Math.ceil(base64.length / CHUNK_BASE64_LENGTH);

    for (let i = 0; i < total; i++) {
        sendToSw({
            action: 'pdfRenderChunk',
            tabId,
            index: i,
            total,
            data: base64.slice(i * CHUNK_BASE64_LENGTH, (i + 1) * CHUNK_BASE64_LENGTH)
        });
    }

    try { await pdf.destroy(); } catch (e) {}
    sendProgress(tabId, 100);
}

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
    if (!message || message.action !== 'renderPdf') return false;
    renderPdf(message)
        .then(() => { if (sendResponse) sendResponse({ ok: true }); })
        .catch((err) => {
            console.error('[pdf-render] Error:', err && err.message ? err.message : err);
            sendToSw({ action: 'pdfRenderError', tabId: message.tabId, error: String((err && err.message) || err) });
            if (sendResponse) sendResponse({ ok: false });
        });
    return true;
});
