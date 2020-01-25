const foreachCollectionSymbol = "$foreachCollectionSymbol"

ko.virtualElements.allowedBindings["dotvvm-SSR-foreach"] = true;
ko.virtualElements.allowedBindings["dotvvm-SSR-item"] = true;

type SeenUpdateElement = HTMLElement & { seenUpdate?: number };

export default {
    "dotvvm-SSR-foreach": {
        init(element: Node, valueAccessor: () => any, allBindings?: KnockoutAllBindingsAccessor, viewModel?: any, bindingContext?: KnockoutBindingContext) {
            if (!bindingContext) {
                throw new Error();
            }

            let savedNodes: Node[] | undefined;
            ko.computed(() => {
                const rawValue = valueAccessor().data;

                // save a copy of the inner nodes on the initial update, but only if we have dependencies.
                if (!savedNodes && ko.computedContext.getDependenciesCount()) {
                    savedNodes = ko.utils.cloneNodes(ko.virtualElements.childNodes(element), true /* shouldCleanNodes */);
                }

                if (rawValue != null) {
                    if (savedNodes) {
                        ko.virtualElements.setDomNodeChildren(element, ko.utils.cloneNodes(savedNodes));
                    }
                    ko.applyBindingsToDescendants(bindingContext.extend({ [foreachCollectionSymbol]: rawValue }), element);
                } else {
                    ko.virtualElements.emptyNode(element);
                }

            }, null, { disposeWhenNodeIsRemoved: element });
            return { controlsDescendantBindings: true } // do not apply binding again
        }
    },
    "dotvvm-SSR-item": {
        init<T>(element: SeenUpdateElement, valueAccessor: () => T, allBindings?: any, viewModel?: any, bindingContext?: KnockoutBindingContext) {
            if (!bindingContext) {
                throw new Error();
            }

            const collection = (bindingContext as any)[foreachCollectionSymbol]
            if (!collection) {
                throw new Error();
            }

            const innerBindingContext = bindingContext.createChildContext(() => {
                    return ko.unwrap((ko.unwrap(collection) || [])[valueAccessor()]);
                }).extend({ $index: ko.pureComputed(valueAccessor) });
            ko.applyBindingsToDescendants(innerBindingContext, element);
            return { controlsDescendantBindings: true }; // do not apply binding again
        },
        update(element: SeenUpdateElement) {
            if (element.seenUpdate) {
                console.error(`dotvvm-SSR-item binding did not expect to see an update`);
            }
            element.seenUpdate = 1;
        }
    }
}
