export function openFolderPicker(container) {
    const input = container?.querySelector('input[type="file"]');
    if (input) {
        input.click();
    }
}

