import { DOM } from './constants.js';

export function showToast(msg, type = 'success') {
    if (!DOM.toast) return;

    // Normalización de nombres legacy
    if (type === 'danger') type = 'error';
    if (type === 'warn') type = 'warning';

    const icons = {
        success: '<span class="toast-icon" style="color:#22c55e;">✅</span>',
        error:   '<span class="toast-icon" style="color:#ef4444;">❌</span>',
        warning: '<span class="toast-icon" style="color:#f59e0b;">⚠️</span>',
        info:    '<span class="toast-icon" style="color:#3b82f6;">ℹ️</span>'
    };
    const icon = icons[type] || icons.success;

    DOM.toast.innerHTML = `${icon} <span>${msg}</span>`;
    DOM.toast.className = type === 'success' ? '' : type; // la clase del tipo

    void DOM.toast.offsetWidth; // forzar reflow para reiniciar animación
    DOM.toast.classList.add('show');

    if (window._toastTimer) clearTimeout(window._toastTimer); // reiniciar el auto-ocultado
    window._toastTimer = setTimeout(() => {
        if (DOM.toast) DOM.toast.classList.remove('show');
    }, 3000);
}