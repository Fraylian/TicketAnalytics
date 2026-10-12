// Módulo ES de descargas. Se importa bajo demanda desde FileDownloadService
// (IJSRuntime.InvokeAsync<IJSObjectReference>("import", ...)), por lo que no
// depende de que un <script> global haya cargado ni de window.*.

/**
 * Descarga un archivo desde un stream .NET (DotNetStreamReference).
 * Evita el rodeo bytes -> base64 -> string -> Uint8Array.
 */
export async function downloadFromStream(fileName, contentType, streamRef) {
    const buffer = await streamRef.arrayBuffer();
    const blob = new Blob([buffer], { type: contentType });
    triggerDownload(fileName, blob);
}

/** Descarga contenido de texto (CSV, TXT, JSON) con BOM para Excel. */
export function downloadText(fileName, contentType, content) {
    const blob = new Blob(["\uFEFF" + content], { type: contentType });
    triggerDownload(fileName, blob);
}

function triggerDownload(fileName, blob) {
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = fileName;
    a.style.display = "none";
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    // Liberamos el ObjectURL en el siguiente tick: revocarlo de inmediato
    // cancela la descarga en algunos navegadores.
    setTimeout(() => URL.revokeObjectURL(url), 10000);
}
