const ILLEGAL_FILENAME_CHARACTERS = /[<>:"/\\|?*\u0000-\u001F\u007F]/g;
const WINDOWS_RESERVED_NAME = /^(?:CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\..*)?$/i;

/** Keeps browser download names local, flat, and safe across common filesystems. */
export function sanitizeDownloadFilename(value: string | null | undefined, fallback: string): string {
    const normalized = (value ?? '')
        .normalize('NFKC')
        .replace(ILLEGAL_FILENAME_CHARACTERS, '-')
        .replace(/\s+/g, '-')
        .replace(/-+/g, '-')
        .replace(/^\.+/, '')
        .replace(/\.+$/, '')
        .replace(/^-+|-+$/g, '')
        .slice(0, 80);
    const safeName = normalized || fallback;

    return WINDOWS_RESERVED_NAME.test(safeName) ? `download-${safeName}` : safeName;
}