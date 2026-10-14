// Módulo ES de microinteracciones. Se importa bajo demanda desde Blazor
// (IJSRuntime.InvokeAsync<IJSObjectReference>("import", "./js/ui.js")).

const reducido = () => window.matchMedia("(prefers-reduced-motion: reduce)").matches;

/**
 * Anima un número de "desde" a "hasta" dentro del elemento.
 * Al terminar deja exactamente el texto final que generó .NET, así que
 * el formato visible siempre coincide con el de la aplicación.
 */
export function animarNumero(el, desde, hasta, decimales, textoFinal, duracion) {
    if (!el) return;
    if (el._taAnim) cancelAnimationFrame(el._taAnim);
    if (reducido() || desde === hasta) { el.textContent = textoFinal; return; }

    const fmt = new Intl.NumberFormat("es-DO", {
        minimumFractionDigits: decimales, maximumFractionDigits: decimales
    });
    const inicio = performance.now();
    const dur = duracion || 900;
    const ease = t => 1 - Math.pow(1 - t, 3);

    const paso = ahora => {
        const t = Math.min(1, (ahora - inicio) / dur);
        const v = desde + (hasta - desde) * ease(t);
        el.textContent = fmt.format(v);
        if (t < 1) el._taAnim = requestAnimationFrame(paso);
        else { el.textContent = textoFinal; el._taAnim = null; }
    };
    el._taAnim = requestAnimationFrame(paso);
}

/** Copia texto al portapapeles con respaldo para contextos sin Clipboard API. */
export async function copiar(texto) {
    try {
        await navigator.clipboard.writeText(texto);
        return true;
    } catch {
        const ta = document.createElement("textarea");
        ta.value = texto;
        ta.style.position = "fixed";
        ta.style.opacity = "0";
        document.body.appendChild(ta);
        ta.select();
        const ok = document.execCommand("copy");
        document.body.removeChild(ta);
        return ok;
    }
}

/** Lleva suavemente la vista a una sección (navegación por bloques). */
export function irA(id) {
    const el = document.getElementById(id);
    if (!el) return;
    const y = el.getBoundingClientRect().top + window.scrollY - 76;
    window.scrollTo({ top: y, behavior: reducido() ? "auto" : "smooth" });
}

/** Estado del permiso de notificaciones del escritorio: granted | denied | default | no-soportado. */
export function permisoNotificaciones() {
    return ("Notification" in window) ? Notification.permission : "no-soportado";
}

/** Pide permiso (debe llamarse desde un clic del usuario para que el navegador lo muestre). */
export async function pedirPermisoNotificaciones() {
    if (!("Notification" in window)) return "no-soportado";
    if (Notification.permission === "default") return await Notification.requestPermission();
    return Notification.permission;
}

/**
 * Notificación del escritorio (fuera de la pestaña). Solo se muestra si el usuario
 * ya dio permiso; si no, la alerta queda dentro de la aplicación.
 */
export function notificar(titulo, cuerpo, etiqueta) {
    if (!("Notification" in window) || Notification.permission !== "granted") return false;
    const n = new Notification(titulo, {
        body: cuerpo, tag: etiqueta, icon: "img/logo-bsc.png", requireInteraction: true
    });
    n.onclick = () => { window.focus(); n.close(); };
    return true;
}


/** Tema actual según la clase de <html> (la pone index.html al cargar): "oscuro" | "claro". */
export function temaActual() {
    return document.documentElement.classList.contains("ta-oscuro") ? "oscuro" : "claro";
}

/** Aplica el tema con una transición corta y guarda la preferencia en el navegador. */
export function aplicarTema(tema) {
    const raiz = document.documentElement;
    raiz.classList.add("ta-cambiando-tema");
    raiz.classList.toggle("ta-oscuro", tema === "oscuro");
    try { localStorage.setItem("ta-tema", tema); } catch (e) { /* sin almacenamiento: dura la sesión */ }
    setTimeout(() => raiz.classList.remove("ta-cambiando-tema"), 320);
}
