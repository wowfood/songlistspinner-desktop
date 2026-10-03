const _spinnerContracts = window.SonglistSpinnerContracts

window.SpinnerInterop = (function () {
    let _wheel = null
    let _isResizing = false
    let _resizePointerId = null
    let _savedWidth = null
    let _savedMinWidth = null
    let _resizeTimeout = null
    let _resizeObserver = null
    let _resizeHandle = null
    let _resizePlayedList = null
    let _resizeDotNetRef = null
    let _winnerDialog = null
    let _winnerDialogCancelHandler = null
    let _winnerDialogDotNetRef = null
    let _winnerReturnFocus = null
    let _wheelItems = []
    let _wheelColors = []

    const wheelLabelRadius = 0.9
    const wheelLabelRadiusMax = 0.08
    const wheelLabelFontSizeMin = 12
    const wheelLabelFontSizeMax = 28
    const resizeMinPixels = 300
    const resizeMaxPixels = 800
    const resizeKeyboardStepPixels = 24

    function calculateWheelLabelFontSize(itemCount) {
        if (itemCount <= 1) return wheelLabelFontSizeMax

        // spin-wheel uses a 500px reference size. Size the font from the chord
        // available at the middle of each slice, then let the library scale it
        // with the rendered wheel.
        const referenceLabelRadius = 250 * 0.95 * 0.55
        const sliceHeight = 2 * referenceLabelRadius * Math.sin(Math.PI / itemCount)
        return Math.max(
            wheelLabelFontSizeMin,
            Math.min(wheelLabelFontSizeMax, Math.floor(sliceHeight * 0.68)))
    }

    function fitWheelLabel(context, label, fontSize, maxWidth) {
        if (!context || typeof label !== 'string') return label || ''

        context.font = `${fontSize}px sans-serif`
        if (context.measureText(label).width <= maxWidth) return label

        const ellipsis = '…'
        let lower = 0
        let upper = label.length
        while (lower < upper) {
            const length = Math.ceil((lower + upper) / 2)
            const candidate = `${label.slice(0, length).trimEnd()}${ellipsis}`
            if (context.measureText(candidate).width <= maxWidth) {
                lower = length
            } else {
                upper = length - 1
            }
        }

        return `${label.slice(0, lower).trimEnd()}${ellipsis}`
    }

    function buildWheel(container) {
        const fontSize = calculateWheelLabelFontSize(_wheelItems.length)
        const referenceRadius = 250 * 0.95
        const maxLabelWidth = referenceRadius * (wheelLabelRadius - wheelLabelRadiusMax)
        const measureContext = document.createElement('canvas').getContext('2d')
        const fittedItems = _wheelItems.map(item => ({
            ...item,
            label: fitWheelLabel(measureContext, item.label, fontSize, maxLabelWidth)
        }))

        return new spinWheel.Wheel(container, {
            items: fittedItems,
            itemBackgroundColors: _wheelColors,
            itemLabelFontSizeMax: fontSize,
            itemLabelRadius: wheelLabelRadius,
            itemLabelRadiusMax: wheelLabelRadiusMax,
            itemLabelStrokeWidth: 1,
            borderWidth: 0,
            lineWidth: 0,
            radius: 0.95,
            isInteractive: false
        })
    }

    function resetResizeInteraction() {
        _isResizing = false
        _resizePointerId = null
        document.body.style.cursor = 'default'
        document.body.style.userSelect = 'auto'
    }

    function detachResizeHandlers() {
        if (_resizeHandle) {
            _resizeHandle.removeEventListener('pointerdown', handleResizePointerDown)
            _resizeHandle.removeEventListener('keydown', handleResizeKeyDown)
        }
        document.removeEventListener('pointermove', handleResizePointerMove)
        document.removeEventListener('pointerup', handleResizePointerUp)
        document.removeEventListener('pointercancel', handleResizePointerUp)
        _resizeHandle = null
        _resizePlayedList = null
        _resizeDotNetRef = null
        resetResizeInteraction()
    }

    function cleanupWinnerDialog(restoreFocus) {
        const dialog = _winnerDialog || document.getElementById('winnerModal')
        const returnFocus = _winnerReturnFocus

        if (dialog && _winnerDialogCancelHandler) {
            dialog.removeEventListener('cancel', _winnerDialogCancelHandler)
        }
        if (dialog && dialog.open) dialog.close()

        _winnerDialog = null
        _winnerDialogCancelHandler = null
        _winnerDialogDotNetRef = null
        _winnerReturnFocus = null

        if (!restoreFocus) return
        const fallback = document.getElementById('spinButton') || document.getElementById('playedListSpinButton')
        const target = returnFocus && returnFocus.isConnected && !returnFocus.disabled
            ? returnFocus
            : fallback
        if (target && !target.disabled) target.focus({ preventScroll: true })
    }

    function applyPlayedListWidth(newWidth) {
        if (!_resizePlayedList || !_resizeHandle) return false
        const container = document.getElementById('container')
        if (!container) return false
        const containerWidth = container.getBoundingClientRect().width
        if (containerWidth <= 0 || newWidth < resizeMinPixels || newWidth > resizeMaxPixels) return false

        const roundedWidth = Math.round(newWidth)
        _resizePlayedList.style.width = `${(newWidth / containerWidth) * 100}%`
        _resizePlayedList.style.minWidth = `${resizeMinPixels}px`
        _resizeHandle.setAttribute('aria-valuenow', `${roundedWidth}`)
        _resizeHandle.setAttribute('aria-valuetext', `${roundedWidth} pixels`)
        return true
    }

    function synchronizePlayedListWidth() {
        if (!_resizePlayedList || !_resizeDotNetRef) return
        const width = _resizePlayedList.style.width
        const minWidth = _resizePlayedList.style.minWidth
        if (!width) return

        _resizeDotNetRef.invokeMethodAsync('OnResizeEnd', width, minWidth)
            .catch(error => console.warn('Unable to synchronize the played-list width.', error))
    }

    function handleResizePointerDown(e) {
        if (e.button !== 0 || !_resizeHandle) return
        e.preventDefault()
        _isResizing = true
        _resizePointerId = e.pointerId
        _resizeHandle.setPointerCapture(e.pointerId)
        document.body.style.cursor = 'ew-resize'
        document.body.style.userSelect = 'none'
    }

    function handleResizePointerMove(e) {
        if (!_isResizing || e.pointerId !== _resizePointerId || !_resizePlayedList) return
        const container = document.getElementById('container')
        if (!container) return

        e.preventDefault()
        const containerRect = container.getBoundingClientRect()
        const position = _resizePlayedList.dataset.position || _spinnerContracts.playedListPositions.default
        const newWidth = position === _spinnerContracts.playedListPositions.left
            ? e.clientX - containerRect.left - 10
            : containerRect.right - e.clientX - 10
        applyPlayedListWidth(newWidth)
    }

    function handleResizePointerUp(e) {
        if (!_isResizing || e.pointerId !== _resizePointerId || !_resizeHandle) return
        if (_resizeHandle.hasPointerCapture(e.pointerId)) {
            _resizeHandle.releasePointerCapture(e.pointerId)
        }
        resetResizeInteraction()
        synchronizePlayedListWidth()
    }

    function handleResizeKeyDown(e) {
        if (!_resizePlayedList) return
        const supportedKeys = ['ArrowLeft', 'ArrowRight', 'Home', 'End']
        if (!supportedKeys.includes(e.key)) return

        e.preventDefault()
        const position = _resizePlayedList.dataset.position || _spinnerContracts.playedListPositions.default
        const currentWidth = _resizePlayedList.getBoundingClientRect().width
        let newWidth
        if (e.key === 'Home') {
            newWidth = resizeMinPixels
        } else if (e.key === 'End') {
            newWidth = resizeMaxPixels
        } else {
            const separatorDirection = e.key === 'ArrowRight' ? 1 : -1
            const panelDirection = position === _spinnerContracts.playedListPositions.left
                ? separatorDirection
                : -separatorDirection
            newWidth = currentWidth + panelDirection * resizeKeyboardStepPixels
        }

        if (applyPlayedListWidth(Math.max(resizeMinPixels, Math.min(resizeMaxPixels, newWidth)))) {
            synchronizePlayedListWidth()
        }
    }

    return {
        createWheel(items, colors) {
            const container = document.getElementById('wheelContainer')
            if (!container) return
            if (!window.spinWheel || !window.spinWheel.Wheel) {
                container.textContent = 'The wheel component could not be loaded.'
                return
            }
            if (_wheel) {
                _wheel.remove();
                _wheel = null
            }
            _wheelItems = Array.isArray(items) ? items : []
            _wheelColors = Array.isArray(colors) ? colors : []
            _wheel = buildWheel(container)
        },

        spinToItem(index, duration) {
            if (_wheel) _wheel.spinToItem(index, duration)
        },

        getItems() {
            return _wheel ? _wheel.items : []
        },

        setupResizeObserver() {
            const container = document.getElementById('wheelContainer')
            if (!container || !window.ResizeObserver) return
            if (_resizeObserver) _resizeObserver.disconnect()
            _resizeObserver = new ResizeObserver(() => {
                if (_wheel && !_isResizing) {
                    clearTimeout(_resizeTimeout)
                    _resizeTimeout = setTimeout(() => {
                        if (_wheel) {
                            _wheel.remove()
                            _wheel = buildWheel(container)
                        }
                    }, 200)
                }
            })
            _resizeObserver.observe(container)
        },

        setupResizeHandlers(dotNetRef) {
            detachResizeHandlers()
            const handle = document.getElementById('resizeHandle')
            const playedList = document.getElementById('playedList')
            if (!handle || !playedList) return

            _resizeHandle = handle
            _resizePlayedList = playedList
            _resizeDotNetRef = dotNetRef
            const currentWidth = Math.round(playedList.getBoundingClientRect().width)
            _resizeHandle.setAttribute('aria-valuenow', `${currentWidth}`)
            _resizeHandle.setAttribute('aria-valuetext', `${currentWidth} pixels`)
            _resizeHandle.addEventListener('pointerdown', handleResizePointerDown)
            _resizeHandle.addEventListener('keydown', handleResizeKeyDown)
            document.addEventListener('pointermove', handleResizePointerMove)
            document.addEventListener('pointerup', handleResizePointerUp)
            document.addEventListener('pointercancel', handleResizePointerUp)
        },

        openWinnerDialog(preferredActionId, dotNetRef) {
            cleanupWinnerDialog(false)
            const dialog = document.getElementById('winnerModal')
            if (!(dialog instanceof HTMLDialogElement)) {
                throw new Error('The winner dialog is unavailable.')
            }

            _winnerDialog = dialog
            _winnerDialogDotNetRef = dotNetRef
            _winnerReturnFocus = document.activeElement instanceof HTMLElement
                ? document.activeElement
                : null
            _winnerDialogCancelHandler = event => {
                event.preventDefault()
                if (!_winnerDialogDotNetRef) return
                _winnerDialogDotNetRef.invokeMethodAsync('OnWinnerDialogCancelled')
                    .catch(error => console.warn('Unable to close the winner dialog.', error))
            }
            dialog.addEventListener('cancel', _winnerDialogCancelHandler)
            dialog.showModal()

            const preferredAction = document.getElementById(preferredActionId)
            const firstEnabledAction = dialog.querySelector('button:not(:disabled)')
            const focusTarget = preferredAction && !preferredAction.disabled
                ? preferredAction
                : firstEnabledAction
            if (focusTarget) focusTarget.focus({ preventScroll: true })
        },

        closeWinnerDialog() {
            cleanupWinnerDialog(true)
        },

        disposeDashboardBindings() {
            detachResizeHandlers()
            cleanupWinnerDialog(false)
            if (_resizeObserver) {
                _resizeObserver.disconnect()
                _resizeObserver = null
            }
            clearTimeout(_resizeTimeout)
            _resizeTimeout = null
        },

        applyTheme(colors, playedList, winnerDialog) {
            const r = document.documentElement
            if (!colors) return
            r.style.setProperty('--app-text-color', colors.text || '')
            r.style.setProperty('--app-status-bg', colors.statusBackground || '')
            r.style.setProperty('--app-played-list-bg', colors.playedListBackground || '')
            r.style.setProperty('--app-now-playing-bg', colors.nowPlayingBackground || colors.playedListBackground || '')
            r.style.setProperty('--app-played-item-bg', colors.playedItemBackground || '')
            r.style.setProperty('--app-resize-handle-bg', colors.resizeHandleBackground || '')
            r.style.setProperty('--app-resize-handle-hover-bg', colors.resizeHandleHoverBackground || '')
            r.style.setProperty('--app-toggle-bg', colors.toggleBackground || '')
            r.style.setProperty('--app-button-bg', colors.buttonBackground || '')
            r.style.setProperty('--app-button-text', colors.buttonText || '')
            r.style.setProperty('--app-pointer-color', colors.pointer || '')
            if (playedList) {
                r.style.setProperty('--app-played-list-font-family', playedList.fontFamily || '')
                r.style.setProperty('--app-played-list-font-size', playedList.fontSize || '')
                r.style.setProperty('--app-played-list-max-lines', playedList.maxLines ?? '')
            }
            if (winnerDialog) {
                r.style.setProperty('--app-winner-dialog-font-family', winnerDialog.fontFamily || '')
                r.style.setProperty('--app-winner-dialog-font-size', winnerDialog.fontSize || '')
                r.style.setProperty('--app-winner-dialog-width', winnerDialog.width || '')
            }
        },

        applyBackground(background) {
            if (!background) return
            const modes = _spinnerContracts.backgroundModes
            const mode = (background.mode || modes.color).toLowerCase()
            document.body.style.backgroundColor = background.color || ''
            if (mode === modes.transparent || mode === modes.legacyTransparent) {
                document.body.style.backgroundColor = modes.transparent
                document.body.style.backgroundImage = 'none'
            } else if (mode === modes.color) {
                document.body.style.backgroundImage = 'none'
            }
        },

        resetBackground() {
            document.body.style.backgroundColor = ''
            document.body.style.backgroundImage = ''
        },

        applyPlayedListPosition(position) {
            const container = document.getElementById('container')
            const icon = document.getElementById('collapseIcon')
            if (!container || !icon) return
            const positions = _spinnerContracts.playedListPositions
            if ((position || positions.default).toLowerCase() === positions.left) {
                container.classList.add('played-list-left')
                icon.innerText = '◀'
            } else {
                container.classList.remove('played-list-left')
                icon.innerText = '▶'
            }
        },

        runConfetti(colors) {
            const el = document.getElementById('winnerConfetti')
            if (!el) return
            el.innerHTML = ''
            if (window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches) return
            const palette = colors || ['#ff6b6b', '#4ecdc4', '#45b7d1', '#f9ca24']
            for (let i = 0; i < 36; i++) {
                const piece = document.createElement('span')
                piece.className = 'winner-confetti-piece'
                piece.style.left = `${Math.random() * 100}%`
                piece.style.backgroundColor = palette[i % palette.length]
                piece.style.animationDelay = `${Math.random() * 0.5}s`
                piece.style.animationDuration = `${1.2 + Math.random() * 1.1}s`
                el.appendChild(piece)
            }
        },

        setWheelVisible(visible) {
            const el = document.getElementById('wheelContents')
            if (el) el.style.display = visible ? 'flex' : 'none'
        },

        setPlayedListCollapsed(collapsed, position) {
            const el = document.getElementById('playedList')
            const icon = document.getElementById('collapseIcon')
            if (!el || !icon) return
            const positions = _spinnerContracts.playedListPositions
            const pos = (position || positions.default).toLowerCase()
            if (collapsed) {
                _savedWidth = el.style.width || ''
                _savedMinWidth = el.style.minWidth || ''
                el.classList.add('collapsed')
                el.style.width = '3rem'
                el.style.minWidth = '3rem'
                icon.innerText = pos === positions.left ? '▶' : '◀'
            } else {
                el.classList.remove('collapsed')
                icon.innerText = pos === positions.left ? '◀' : '▶'
                el.style.width = _savedWidth || ''
                el.style.minWidth = _savedMinWidth || ''
            }
        },

        setPlayedListWidth(width, minWidth) {
            const el = document.getElementById('playedList')
            if (el) {
                el.style.width = width
                el.style.minWidth = minWidth
            }
        },

        updateSettingsPreview(frameId, payload) {
            const frame = document.getElementById(frameId)
            if (!frame || !frame.contentWindow) return

            let targetOrigin = '*'
            try {
                targetOrigin = new URL(frame.src).origin
            } catch {
                // The preview is always a local frame; '*' is only a defensive fallback.
            }

            frame.contentWindow.postMessage({
                type: _spinnerContracts.messageTypes.settingsPreview,
                payload
            }, targetOrigin)
        },

        validateCssSettings(request) {
            const errors = {}
            const forbiddenSizingValues = new Set([
                'auto', 'inherit', 'initial', 'unset', 'revert', 'revert-layer',
                'fit-content', 'max-content', 'min-content', 'normal'
            ])

            for (const field of request && request.sizes || []) {
                const value = String(field.value || '').trim()
                const property = field.property === 'width' ? 'width' : 'font-size'
                if (!value) {
                    errors[field.key] = `Enter a ${field.label.toLowerCase()}.`
                    continue
                }
                if (forbiddenSizingValues.has(value.toLowerCase()) ||
                    value.toLowerCase().includes('var(') ||
                    !CSS.supports(property, value)) {
                    errors[field.key] = `${field.label} must be a concrete CSS size such as ${property === 'width' ? '28rem or 480px' : '1rem or 16px'}.`
                    continue
                }

                const probe = document.createElement('div')
                probe.style.position = 'fixed'
                probe.style.visibility = 'hidden'
                probe.style[property] = value
                document.body.appendChild(probe)
                const computedValue = Number.parseFloat(getComputedStyle(probe)[property === 'width' ? 'width' : 'fontSize'])
                probe.remove()
                if (!Number.isFinite(computedValue) || computedValue <= 0) {
                    errors[field.key] = `${field.label} must resolve to a size greater than zero.`
                }
            }

            for (const field of request && request.colorLists || []) {
                const values = Array.isArray(field.values) ? field.values : []
                if (values.length === 0) {
                    errors[field.key] = `Enter at least one ${field.label.toLowerCase()}.`
                    continue
                }

                const invalidLines = values
                    .map((value, index) => ({ value: String(value || '').trim(), line: index + 1 }))
                    .filter(item => !item.value ||
                        item.value.toLowerCase().includes('var(') ||
                        !CSS.supports('color', item.value))
                if (invalidLines.length > 0) {
                    const lines = invalidLines.slice(0, 3).map(item => item.line).join(', ')
                    errors[field.key] = `Use valid CSS colors on line${invalidLines.length === 1 ? '' : 's'} ${lines}${invalidLines.length > 3 ? ', …' : ''}.`
                }
            }

            return errors
        }
    }
})()
