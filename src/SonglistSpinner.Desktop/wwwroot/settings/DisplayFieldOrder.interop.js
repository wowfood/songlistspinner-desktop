export function initialize(container, dotNetReference) {
    if (!window.Sortable) {
        throw new Error("SortableJS is not loaded.");
    }

    let disposed = false;
    const sortable = window.Sortable.create(container, {
        animation: 150,
        chosenClass: "ss-chip-sortable-chosen",
        dragClass: "ss-chip-sortable-drag",
        draggable: ".ss-chip",
        fallbackClass: "ss-chip-sortable-fallback",
        fallbackOnBody: true,
        fallbackTolerance: 4,
        forceFallback: true,
        ghostClass: "ss-chip-sortable-ghost",
        handle: ".ss-chip-handle",
        scroll: true,
        onUpdate: event => {
            const oldIndex = event.oldDraggableIndex;
            const newIndex = event.newDraggableIndex;
            const fieldName = event.item.dataset.fieldName;

            if (!Number.isInteger(oldIndex) ||
                !Number.isInteger(newIndex) ||
                !fieldName ||
                oldIndex === newIndex) {
                return;
            }

            // Sortable mutates the DOM first. Restore the original position immediately
            // so Blazor remains the DOM owner, then let the component re-render from state.
            const siblings = Array.from(event.from.children)
                .filter(element => element.matches(".ss-chip") && element !== event.item);
            event.from.insertBefore(event.item, siblings[oldIndex] ?? null);

            dotNetReference.invokeMethodAsync("OnFieldReordered", fieldName, newIndex)
                .catch(error => {
                    if (!disposed) {
                        console.error("Unable to reorder display field.", error);
                    }
                });
        }
    });

    return {
        dispose() {
            disposed = true;
            sortable.destroy();
        }
    };
}
