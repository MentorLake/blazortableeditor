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
					return;
				}

				const fillHandle = e.target.closest && e.target.closest(".bte-fill-handle");
				if (fillHandle) {
					e.preventDefault();
					e.stopPropagation();
					self._beginFillDrag();
					return;
				}

				const dropdown = e.target.closest && e.target.closest("select.bte-cell-select");
				if (dropdown) {
					const cell = dropdown.closest(".bte-cell");
					if (cell && self._viewport.contains(cell)) {
						const row = parseInt(cell.getAttribute("data-row"), 10);
						const col = parseInt(cell.getAttribute("data-col"), 10);
						if (Number.isFinite(row) && Number.isFinite(col) && self._dotNetRef) {
							self._dotNetRef.invokeMethodAsync("OnDropdownCellActivate", row, col, !!e.shiftKey).catch(() => {});
						}
					}
					return;
				}

				const cell = e.target.closest && e.target.closest(".bte-cell");
				if (cell && self._viewport.contains(cell)) {
					const row = parseInt(cell.getAttribute("data-row"), 10);
					const col = parseInt(cell.getAttribute("data-col"), 10);
					if (!Number.isFinite(row) || !Number.isFinite(col)) return;
					e.preventDefault();
					e.stopPropagation();
					self._beginSelectionDrag(row, col, !!e.shiftKey);
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
			this._stopSelectionDrag(false);
			this._stopFillDrag(false);
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

		_getCellTrack: function () {
			const canvas = this._viewport ? this._viewport.querySelector(".bte-canvas") : null;
			return canvas ? canvas.querySelector(".bte-cell-track") : null;
		},

		_cellAtPoint: function (clientX, clientY) {
			const prev = this._selectionOverlay;
			const prevPe = prev ? prev.style.pointerEvents : null;
			const fillPrev = this._fillPreviewEl;
			const fillPe = fillPrev ? fillPrev.style.pointerEvents : null;
			if (prev) prev.style.pointerEvents = "none";
			if (fillPrev) fillPrev.style.pointerEvents = "none";

			const el = document.elementFromPoint(clientX, clientY);
			if (prev) prev.style.pointerEvents = prevPe || "";
			if (fillPrev) fillPrev.style.pointerEvents = fillPe || "";

			const cell = el && el.closest ? el.closest(".bte-cell") : null;
			if (!cell || !this._viewport || !this._viewport.contains(cell)) return null;
			const row = parseInt(cell.getAttribute("data-row"), 10);
			const col = parseInt(cell.getAttribute("data-col"), 10);
			if (!Number.isFinite(row) || !Number.isFinite(col)) return null;
			return {el: cell, row, col};
		},

		_ensureSelectionOverlay: function (cellTrack) {
			let sel = cellTrack.querySelector(":scope > .bte-selection");
			if (!sel) {
				sel = document.createElement("div");
				sel.className = "bte-selection";
				const handle = document.createElement("div");
				handle.className = "bte-fill-handle";
				sel.appendChild(handle);
				cellTrack.appendChild(sel);
			}
			this._selectionOverlay = sel;
			return sel;
		},

		_ensureFillPreview: function (cellTrack) {
			let el = cellTrack.querySelector(":scope > .bte-fill-preview");
			if (!el) {
				el = document.createElement("div");
				el.className = "bte-fill-preview";
				cellTrack.appendChild(el);
			}
			this._fillPreviewEl = el;
			return el;
		},

		_regionBoxFromCells: function (cellTrack, minR, maxR, minC, maxC) {
			const cells = cellTrack.querySelectorAll(".bte-cell");
			let left = Infinity;
			let top = Infinity;
			let right = -Infinity;
			let bottom = -Infinity;
			let found = false;

			for (let i = 0; i < cells.length; i++) {
				const cell = cells[i];
				const r = parseInt(cell.getAttribute("data-row"), 10);
				const c = parseInt(cell.getAttribute("data-col"), 10);
				if (r < minR || r > maxR || c < minC || c > maxC) continue;
				found = true;
				const L = stylePx(cell, "left");
				const T = stylePx(cell, "top");
				const W = stylePx(cell, "width");
				const H = stylePx(cell, "height");
				if (L < left) left = L;
				if (T < top) top = T;
				if (L + W > right) right = L + W;
				if (T + H > bottom) bottom = T + H;
			}

			if (!found) return null;
			return {left, top, width: right - left, height: bottom - top};
		},

		_applySelectionVisual: function () {
			const cellTrack = this._getCellTrack();
			if (!cellTrack) return;

			const minR = Math.min(this._selAnchorRow, this._selEndRow);
			const maxR = Math.max(this._selAnchorRow, this._selEndRow);
			const minC = Math.min(this._selAnchorCol, this._selEndCol);
			const maxC = Math.max(this._selAnchorCol, this._selEndCol);
			const activeR = this._selAnchorRow;
			const activeC = this._selAnchorCol;

			const cells = cellTrack.querySelectorAll(".bte-cell");
			for (let i = 0; i < cells.length; i++) {
				const cell = cells[i];
				const r = parseInt(cell.getAttribute("data-row"), 10);
				const c = parseInt(cell.getAttribute("data-col"), 10);
				const selected = r >= minR && r <= maxR && c >= minC && c <= maxC;
				cell.classList.toggle("is-selected", selected);
				cell.classList.toggle("is-active", r === activeR && c === activeC);
				cell.classList.remove("is-fill");
			}

			const box = this._regionBoxFromCells(cellTrack, minR, maxR, minC, maxC);
			const sel = this._ensureSelectionOverlay(cellTrack);
			if (box) {
				sel.style.left = box.left + "px";
				sel.style.top = box.top + "px";
				sel.style.width = box.width + "px";
				sel.style.height = box.height + "px";
				sel.style.display = "";
			}

			if (this._fillPreviewEl) {
				this._fillPreviewEl.style.display = "none";
			}
		},

		_applyFillVisual: function () {
			const cellTrack = this._getCellTrack();
			if (!cellTrack || !this._fillSource) return;

			const src = this._fillSource;
			const minR = Math.min(src.minR, this._fillEndRow);
			const maxR = Math.max(src.maxR, this._fillEndRow);
			const minC = Math.min(src.minC, this._fillEndCol);
			const maxC = Math.max(src.maxC, this._fillEndCol);

			const cells = cellTrack.querySelectorAll(".bte-cell");
			for (let i = 0; i < cells.length; i++) {
				const cell = cells[i];
				const r = parseInt(cell.getAttribute("data-row"), 10);
				const c = parseInt(cell.getAttribute("data-col"), 10);
				const inSource = r >= src.minR && r <= src.maxR && c >= src.minC && c <= src.maxC;
				const inPreview = r >= minR && r <= maxR && c >= minC && c <= maxC;
				cell.classList.toggle("is-fill", inPreview && !inSource);
			}

			const box = this._regionBoxFromCells(cellTrack, minR, maxR, minC, maxC);
			const preview = this._ensureFillPreview(cellTrack);
			if (box) {
				preview.style.left = box.left + "px";
				preview.style.top = box.top + "px";
				preview.style.width = box.width + "px";
				preview.style.height = box.height + "px";
				preview.style.display = "";
			}
		},

		_beginSelectionDrag: function (row, col, shiftKey) {
			this._stopSelectionDrag(false);
			this._stopFillDrag(false);
			this._stopResize();

			this._selecting = true;
			const root = this._getRoot();
			if (shiftKey && root) {
				const ar = parseInt(root.getAttribute("data-anchor-row"), 10);
				const ac = parseInt(root.getAttribute("data-anchor-col"), 10);
				this._selAnchorRow = Number.isFinite(ar) ? ar : row;
				this._selAnchorCol = Number.isFinite(ac) ? ac : col;
			} else {
				this._selAnchorRow = row;
				this._selAnchorCol = col;
			}
			this._selEndRow = row;
			this._selEndCol = col;
			this._applySelectionVisual();

			if (this._dotNetRef) {
				this._dotNetRef.invokeMethodAsync("OnSelectionDragBegin", row, col, !!shiftKey).catch(() => {});
			}

			const onMove = (e) => {
				e.preventDefault();
				this._selClientX = e.clientX;
				this._selClientY = e.clientY;
				if (!this._selRaf) {
					this._selRaf = requestAnimationFrame(() => {
						this._selRaf = 0;
						const hit = this._cellAtPoint(this._selClientX, this._selClientY);
						if (!hit) return;
						if (hit.row === this._selEndRow && hit.col === this._selEndCol) return;
						this._selEndRow = hit.row;
						this._selEndCol = hit.col;
						this._applySelectionVisual();
					});
				}
			};

			const onUp = (e) => {
				e.preventDefault();
				const hit = this._cellAtPoint(e.clientX, e.clientY);
				if (hit) {
					this._selEndRow = hit.row;
					this._selEndCol = hit.col;
					this._applySelectionVisual();
				}
				this._stopSelectionDrag(true);
			};

			this._selMove = onMove;
			this._selUp = onUp;
			document.addEventListener("mousemove", onMove, {passive: false});
			document.addEventListener("mouseup", onUp, {passive: false});
		},

		_stopSelectionDrag: function (commit) {
			if (this._selRaf) {
				cancelAnimationFrame(this._selRaf);
				this._selRaf = 0;
			}
			if (this._selMove) {
				document.removeEventListener("mousemove", this._selMove);
				this._selMove = null;
			}
			if (this._selUp) {
				document.removeEventListener("mouseup", this._selUp);
				this._selUp = null;
			}

			if (!this._selecting) return;
			this._selecting = false;

			if (commit && this._dotNetRef) {
				this._dotNetRef.invokeMethodAsync(
					"OnSelectionDragEnd",
					this._selEndRow,
					this._selEndCol
				).catch(() => {});
			}
		},

		_beginFillDrag: function () {
			this._stopSelectionDrag(false);
			this._stopFillDrag(false);
			this._stopResize();

			const cellTrack = this._getCellTrack();
			if (!cellTrack) return;

			const root = this._getRoot();
			let minR = parseInt(root && root.getAttribute("data-sel-r0"), 10);
			let maxR = parseInt(root && root.getAttribute("data-sel-r1"), 10);
			let minC = parseInt(root && root.getAttribute("data-sel-c0"), 10);
			let maxC = parseInt(root && root.getAttribute("data-sel-c1"), 10);

			if (![minR, maxR, minC, maxC].every(Number.isFinite)) {
				const active = cellTrack.querySelector(".bte-cell.is-active");
				if (!active) return;
				minR = maxR = parseInt(active.getAttribute("data-row"), 10);
				minC = maxC = parseInt(active.getAttribute("data-col"), 10);
			}

			this._filling = true;
			this._fillSource = {minR, maxR, minC, maxC};
			this._fillEndRow = maxR;
			this._fillEndCol = maxC;
			this._applyFillVisual();

			if (this._dotNetRef) {
				this._dotNetRef.invokeMethodAsync("OnFillDragBegin").catch(() => {});
			}

			const onMove = (e) => {
				e.preventDefault();
				this._fillClientX = e.clientX;
				this._fillClientY = e.clientY;
				if (!this._fillRaf) {
					this._fillRaf = requestAnimationFrame(() => {
						this._fillRaf = 0;
						const hit = this._cellAtPoint(this._fillClientX, this._fillClientY);
						if (!hit) return;
						if (hit.row === this._fillEndRow && hit.col === this._fillEndCol) return;
						this._fillEndRow = hit.row;
						this._fillEndCol = hit.col;
						this._applyFillVisual();
					});
				}
			};

			const onUp = (e) => {
				e.preventDefault();
				const hit = this._cellAtPoint(e.clientX, e.clientY);
				if (hit) {
					this._fillEndRow = hit.row;
					this._fillEndCol = hit.col;
					this._applyFillVisual();
				}
				this._stopFillDrag(true);
			};

			this._fillMove = onMove;
			this._fillUp = onUp;
			document.addEventListener("mousemove", onMove, {passive: false});
			document.addEventListener("mouseup", onUp, {passive: false});
		},

		_stopFillDrag: function (commit) {
			if (this._fillRaf) {
				cancelAnimationFrame(this._fillRaf);
				this._fillRaf = 0;
			}
			if (this._fillMove) {
				document.removeEventListener("mousemove", this._fillMove);
				this._fillMove = null;
			}
			if (this._fillUp) {
				document.removeEventListener("mouseup", this._fillUp);
				this._fillUp = null;
			}

			if (!this._filling) return;
			this._filling = false;

			if (commit && this._dotNetRef) {
				this._dotNetRef.invokeMethodAsync(
					"OnFillDragEnd",
					this._fillEndRow,
					this._fillEndCol
				).catch(() => {});
			}

			this._fillSource = null;
			if (this._fillPreviewEl) {
				this._fillPreviewEl.style.display = "none";
			}
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
