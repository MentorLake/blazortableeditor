export function createInstance() {
	const parsePx = (value) => {
		const n = parseFloat(value);
		return Number.isFinite(n) ? n : 0;
	};

	const stylePx = (el, prop) => (el ? parsePx(el.style[prop]) : 0);

	return {
		init: function (viewport, dotNetRef) {
			this.dispose();

			if (!viewport) {
				return false;
			}

			this._viewport = viewport;
			this._dotNetRef = dotNetRef || null;
			this._rafNotify = 0;
			this._pendingNotify = false;
			this._resizeRaf = 0;
			this._resizePending = null;

			const self = this;

			const notify = function () {
				self._pendingNotify = false;
				self._rafNotify = 0;
				if (!self._dotNetRef || !self._viewport) return;
				const vp = self._viewport;
				self._dotNetRef.invokeMethodAsync(
					"OnViewportMetrics",
					vp.clientWidth,
					vp.clientHeight,
					vp.scrollLeft,
					vp.scrollTop
				);
			};

			const onScroll = function () {
				if (!self._pendingNotify) {
					self._pendingNotify = true;
					self._rafNotify = requestAnimationFrame(notify);
				}
			};

			const onPointerDown = function (e) {
				if (e.button !== 0) return;
				const colHandle = e.target.closest && e.target.closest(".bte-resize-col");
				if (colHandle) {
					e.preventDefault();
					e.stopPropagation();
					const header = colHandle.closest(".bte-col-header");
					if (!header) return;
					const col = parseInt(header.getAttribute("data-col"), 10);
					if (!Number.isFinite(col)) return;
					const startWidth = stylePx(header, "width") || 100;
					self._beginColumnResize(col, e.clientX, startWidth);
					return;
				}

				const rowHandle = e.target.closest && e.target.closest(".bte-resize-row");
				if (rowHandle) {
					e.preventDefault();
					e.stopPropagation();
					const header = rowHandle.closest(".bte-row-header");
					if (!header) return;
					const row = parseInt(header.getAttribute("data-row"), 10);
					if (!Number.isFinite(row)) return;
					const startHeight = stylePx(header, "height") || 28;
					self._beginRowResize(row, e.clientY, startHeight);
				}
			};

			viewport.addEventListener("scroll", onScroll, {passive: true});
			viewport.addEventListener("mousedown", onPointerDown, true);
			this._onScroll = onScroll;
			this._onPointerDown = onPointerDown;

			if (typeof ResizeObserver !== "undefined") {
				const ro = new ResizeObserver(function () {
					notify();
				});
				ro.observe(viewport);
				this._ro = ro;
			}

			notify();
			return true;
		},

		getMetrics: function (viewport) {
			const el = viewport || this._viewport;
			if (!el) return [800, 500, 0, 0];
			return [el.clientWidth, el.clientHeight, el.scrollLeft, el.scrollTop];
		},

		dispose: function () {
			if (this._viewport && this._onScroll) {
				this._viewport.removeEventListener("scroll", this._onScroll);
			}
			if (this._viewport && this._onPointerDown) {
				this._viewport.removeEventListener("mousedown", this._onPointerDown, true);
			}
			if (this._ro) {
				this._ro.disconnect();
				this._ro = null;
			}
			if (this._rafNotify) {
				cancelAnimationFrame(this._rafNotify);
				this._rafNotify = 0;
			}
			this._stopResize();
			this._viewport = null;
			this._dotNetRef = null;
			this._onScroll = null;
			this._onPointerDown = null;
			this._pendingNotify = false;
		},

		startColumnResize: function (dotNetRef, columnIndex, startClientX, startWidth) {
			if (dotNetRef) this._dotNetRef = dotNetRef;
			this._beginColumnResize(columnIndex, startClientX, startWidth);
		},

		startRowResize: function (dotNetRef, rowIndex, startClientY, startHeight) {
			if (dotNetRef) this._dotNetRef = dotNetRef;
			this._beginRowResize(rowIndex, startClientY, startHeight);
		},

		_getRoot: function () {
			return this._viewport ? this._viewport.closest(".bte-root") : null;
		},

		_beginColumnResize: function (columnIndex, startClientX, startWidth) {
			this._stopResize();

			const canvas = this._viewport ? this._viewport.querySelector(".bte-canvas") : null;
			const root = this._getRoot();
			if (root) root.classList.add("is-col-resizing");

			const dotNetRef = this._dotNetRef;
			if (dotNetRef) {
				dotNetRef.invokeMethodAsync("OnColumnResizeBegin", columnIndex);
			}

			if (!canvas) {
				const onUp = (e) => {
					e.preventDefault();
					this._stopResize();
					const width = Math.max(40, Math.round(startWidth + (e.clientX - startClientX)));
					if (dotNetRef) dotNetRef.invokeMethodAsync("OnColumnResizeEnd", columnIndex, width);
				};
				this._resizeUp = onUp;
				document.addEventListener("mouseup", onUp);
				return;
			}

			const headerRow = canvas.querySelector(".bte-header-row");
			const colTrack = canvas.querySelector(".bte-col-track");
			const bodyArea = canvas.querySelector(".bte-body-area");
			const cellTrack = canvas.querySelector(".bte-cell-track");
			const overlays = canvas.querySelectorAll(".bte-selection, .bte-fill-preview, .bte-clipboard-source, .bte-editor");

			const initialCanvasW = stylePx(canvas, "width");
			const initialHeaderRowW = stylePx(headerRow, "width");
			const initialColTrackW = stylePx(colTrack, "width");
			const initialBodyW = stylePx(bodyArea, "width");
			const initialCellTrackW = stylePx(cellTrack, "width");

			const colSel = `[data-col="${columnIndex}"]`;
			const ownHeaders = Array.from(canvas.querySelectorAll(".bte-col-header" + colSel));
			const ownCells = Array.from(canvas.querySelectorAll(".bte-cell" + colSel));
			const colLeft = ownHeaders.length ? stylePx(ownHeaders[0], "left") : 0;

			const laterHeaders = Array.from(canvas.querySelectorAll(".bte-col-header"))
				.filter((el) => parseInt(el.getAttribute("data-col"), 10) > columnIndex)
				.map((el) => ({el, left: stylePx(el, "left")}));
			const laterCells = Array.from(canvas.querySelectorAll(".bte-cell"))
				.filter((el) => parseInt(el.getAttribute("data-col"), 10) > columnIndex)
				.map((el) => ({el, left: stylePx(el, "left")}));

			const overlaySnaps = Array.from(overlays).map((el) => ({
				el,
				left: stylePx(el, "left"),
				width: stylePx(el, "width")
			}));

			let pendingWidth = startWidth;
			let lastApplied = startWidth;

			const apply = (width) => {
				const w = Math.max(40, width);
				if (w === lastApplied) return;
				lastApplied = w;
				const delta = w - startWidth;
				const ww = w + "px";

				for (let i = 0; i < ownHeaders.length; i++) ownHeaders[i].style.width = ww;
				for (let i = 0; i < ownCells.length; i++) ownCells[i].style.width = ww;
				for (let i = 0; i < laterHeaders.length; i++) {
					const s = laterHeaders[i];
					s.el.style.left = (s.left + delta) + "px";
					s.el.style.transform = "";
				}
				for (let i = 0; i < laterCells.length; i++) {
					const s = laterCells[i];
					s.el.style.left = (s.left + delta) + "px";
					s.el.style.transform = "";
				}

				if (canvas) canvas.style.width = (initialCanvasW + delta) + "px";
				if (headerRow) headerRow.style.width = (initialHeaderRowW + delta) + "px";
				if (colTrack) colTrack.style.width = (initialColTrackW + delta) + "px";
				if (bodyArea) bodyArea.style.width = (initialBodyW + delta) + "px";
				if (cellTrack) cellTrack.style.width = (initialCellTrackW + delta) + "px";

				for (let i = 0; i < overlaySnaps.length; i++) {
					const s = overlaySnaps[i];
					const right = s.left + s.width;
					if (right <= colLeft + 0.5) continue;
					s.el.style.transform = "";
					if (s.left >= colLeft + startWidth - 0.5) {
						s.el.style.left = (s.left + delta) + "px";
					} else {
						s.el.style.width = (s.width + delta) + "px";
					}
				}
			};

			const flush = () => {
				this._resizeRaf = 0;
				apply(pendingWidth);
			};

			const onMove = (e) => {
				e.preventDefault();
				pendingWidth = startWidth + (e.clientX - startClientX);
				if (!this._resizeRaf) {
					this._resizeRaf = requestAnimationFrame(flush);
				}
			};

			const onUp = (e) => {
				e.preventDefault();
				if (this._resizeRaf) {
					cancelAnimationFrame(this._resizeRaf);
					this._resizeRaf = 0;
				}
				const finalWidth = Math.max(40, Math.round(startWidth + (e.clientX - startClientX)));
				lastApplied = NaN;
				apply(finalWidth);
				this._endResizeInteraction(dotNetRef, "OnColumnResizeEnd", columnIndex, finalWidth);
			};

			this._resizeMove = onMove;
			this._resizeUp = onUp;
			this._resizeCursor = document.body.style.cursor;
			this._resizeUserSelect = document.body.style.userSelect;
			document.body.style.cursor = "col-resize";
			document.body.style.userSelect = "none";
			document.addEventListener("mousemove", onMove, {passive: false});
			document.addEventListener("mouseup", onUp, {passive: false});
		},

		_beginRowResize: function (rowIndex, startClientY, startHeight) {
			this._stopResize();

			const canvas = this._viewport ? this._viewport.querySelector(".bte-canvas") : null;
			const root = this._getRoot();
			if (root) root.classList.add("is-row-resizing");

			const dotNetRef = this._dotNetRef;
			if (dotNetRef) {
				dotNetRef.invokeMethodAsync("OnRowResizeBegin", rowIndex);
			}

			if (!canvas) {
				const onUp = (e) => {
					e.preventDefault();
					this._stopResize();
					const height = Math.max(18, Math.round(startHeight + (e.clientY - startClientY)));
					if (dotNetRef) dotNetRef.invokeMethodAsync("OnRowResizeEnd", rowIndex, height);
				};
				this._resizeUp = onUp;
				document.addEventListener("mouseup", onUp);
				return;
			}

			const bodyArea = canvas.querySelector(".bte-body-area");
			const rowTrack = canvas.querySelector(".bte-row-track");
			const cellTrack = canvas.querySelector(".bte-cell-track");
			const overlays = canvas.querySelectorAll(".bte-selection, .bte-fill-preview, .bte-clipboard-source, .bte-editor");

			const initialCanvasH = stylePx(canvas, "height");
			const initialBodyH = stylePx(bodyArea, "height");
			const initialRowTrackH = stylePx(rowTrack, "height");
			const initialCellTrackH = stylePx(cellTrack, "height");

			const rowSel = `[data-row="${rowIndex}"]`;
			const ownRowHeaders = Array.from(canvas.querySelectorAll(".bte-row-header" + rowSel));
			const ownCells = Array.from(canvas.querySelectorAll(".bte-cell" + rowSel));
			const rowTop = ownRowHeaders.length ? stylePx(ownRowHeaders[0], "top") : 0;

			const laterRows = Array.from(canvas.querySelectorAll(".bte-row-header"))
				.filter((el) => parseInt(el.getAttribute("data-row"), 10) > rowIndex)
				.map((el) => ({el, top: stylePx(el, "top")}));
			const laterCells = Array.from(canvas.querySelectorAll(".bte-cell"))
				.filter((el) => parseInt(el.getAttribute("data-row"), 10) > rowIndex)
				.map((el) => ({el, top: stylePx(el, "top")}));

			const overlaySnaps = Array.from(overlays).map((el) => ({
				el,
				top: stylePx(el, "top"),
				height: stylePx(el, "height")
			}));

			let pendingHeight = startHeight;
			let lastApplied = startHeight;

			const apply = (height) => {
				const h = Math.max(18, height);
				if (h === lastApplied) return;
				lastApplied = h;
				const delta = h - startHeight;
				const hp = h + "px";

				for (let i = 0; i < ownRowHeaders.length; i++) {
					ownRowHeaders[i].style.height = hp;
					ownRowHeaders[i].style.lineHeight = hp;
				}
				for (let i = 0; i < ownCells.length; i++) ownCells[i].style.height = hp;
				for (let i = 0; i < laterRows.length; i++) {
					const s = laterRows[i];
					s.el.style.top = (s.top + delta) + "px";
					s.el.style.transform = "";
				}
				for (let i = 0; i < laterCells.length; i++) {
					const s = laterCells[i];
					s.el.style.top = (s.top + delta) + "px";
					s.el.style.transform = "";
				}

				if (canvas) canvas.style.height = (initialCanvasH + delta) + "px";
				if (bodyArea) bodyArea.style.height = (initialBodyH + delta) + "px";
				if (rowTrack) rowTrack.style.height = (initialRowTrackH + delta) + "px";
				if (cellTrack) cellTrack.style.height = (initialCellTrackH + delta) + "px";

				for (let i = 0; i < overlaySnaps.length; i++) {
					const s = overlaySnaps[i];
					const bottom = s.top + s.height;
					if (bottom <= rowTop + 0.5) continue;
					s.el.style.transform = "";
					if (s.top >= rowTop + startHeight - 0.5) {
						s.el.style.top = (s.top + delta) + "px";
					} else {
						s.el.style.height = (s.height + delta) + "px";
					}
				}
			};

			const flush = () => {
				this._resizeRaf = 0;
				apply(pendingHeight);
			};

			const onMove = (e) => {
				e.preventDefault();
				pendingHeight = startHeight + (e.clientY - startClientY);
				if (!this._resizeRaf) {
					this._resizeRaf = requestAnimationFrame(flush);
				}
			};

			const onUp = (e) => {
				e.preventDefault();
				if (this._resizeRaf) {
					cancelAnimationFrame(this._resizeRaf);
					this._resizeRaf = 0;
				}
				const finalHeight = Math.max(18, Math.round(startHeight + (e.clientY - startClientY)));
				lastApplied = NaN;
				apply(finalHeight);
				this._endResizeInteraction(dotNetRef, "OnRowResizeEnd", rowIndex, finalHeight);
			};

			this._resizeMove = onMove;
			this._resizeUp = onUp;
			this._resizeCursor = document.body.style.cursor;
			this._resizeUserSelect = document.body.style.userSelect;
			document.body.style.cursor = "row-resize";
			document.body.style.userSelect = "none";
			document.addEventListener("mousemove", onMove, {passive: false});
			document.addEventListener("mouseup", onUp, {passive: false});
		},

		_detachResizeListeners: function () {
			if (this._resizeRaf) {
				cancelAnimationFrame(this._resizeRaf);
				this._resizeRaf = 0;
			}
			if (this._resizeMove) {
				document.removeEventListener("mousemove", this._resizeMove);
				this._resizeMove = null;
			}
			if (this._resizeUp) {
				document.removeEventListener("mouseup", this._resizeUp);
				this._resizeUp = null;
			}
			if (this._resizeCursor !== undefined) {
				document.body.style.cursor = this._resizeCursor;
				this._resizeCursor = undefined;
			}
			if (this._resizeUserSelect !== undefined) {
				document.body.style.userSelect = this._resizeUserSelect;
				this._resizeUserSelect = undefined;
			}
		},

		clearResizeClasses: function () {
			const root = this._getRoot();
			if (root) {
				root.classList.remove("is-col-resizing");
				root.classList.remove("is-row-resizing");
			}
		},

		_endResizeInteraction: function (dotNetRef, method, index, size) {
			this._detachResizeListeners();
			if (dotNetRef) {
				dotNetRef.invokeMethodAsync(method, index, size).catch(() => {});
			} else {
				this.clearResizeClasses();
			}
		},

		_stopResize: function () {
			this._detachResizeListeners();
			this.clearResizeClasses();
		},

		writeText: async function (text) {
			try {
				if (navigator.clipboard && navigator.clipboard.writeText) {
					await navigator.clipboard.writeText(text ?? "");
					return true;
				}
			} catch (_) {
			}

			try {
				const ta = document.createElement("textarea");
				ta.value = text ?? "";
				ta.setAttribute("readonly", "");
				ta.style.position = "fixed";
				ta.style.left = "-9999px";
				document.body.appendChild(ta);
				ta.select();
				const ok = document.execCommand("copy");
				document.body.removeChild(ta);
				return ok;
			} catch (_) {
				return false;
			}
		},

		readText: async function () {
			try {
				if (navigator.clipboard && navigator.clipboard.readText) {
					return await navigator.clipboard.readText();
				}
			} catch (_) {
			}
			return null;
		},

		downloadText: function (filename, text, mimeType) {
			const blob = new Blob([text ?? ""], {
				type: mimeType || "text/plain;charset=utf-8"
			});
			const url = URL.createObjectURL(blob);
			const a = document.createElement("a");
			a.href = url;
			a.download = filename || "download.txt";
			document.body.appendChild(a);
			a.click();
			document.body.removeChild(a);
			URL.revokeObjectURL(url);
		},

		clickElement: function (id) {
			const el = document.getElementById(id);
			if (el) {
				el.click();
			}
		},

		setScroll: function (viewport, left, top) {
			if (!viewport) return;
			viewport.scrollLeft = left || 0;
			viewport.scrollTop = top || 0;
		}
	};
}
