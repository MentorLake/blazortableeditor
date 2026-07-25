window.bteGrid = {
  init: function (viewport, dotNetRef) {
    this.dispose();

    if (!viewport) {
      return false;
    }

    this._viewport = viewport;
    this._dotNetRef = dotNetRef || null;
    this._rafNotify = 0;
    this._pendingNotify = false;

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

    viewport.addEventListener("scroll", onScroll, { passive: true });
    this._onScroll = onScroll;

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
    this._pendingNotify = false;
  },

  startColumnResize: function (dotNetRef, columnIndex, startClientX, startWidth) {
    this._stopResize();

    const onMove = (e) => {
      e.preventDefault();
      const width = Math.max(40, startWidth + (e.clientX - startClientX));
      dotNetRef.invokeMethodAsync("OnColumnResizeMove", columnIndex, width);
    };

    const onUp = (e) => {
      e.preventDefault();
      this._stopResize();
      const width = Math.max(40, startWidth + (e.clientX - startClientX));
      dotNetRef.invokeMethodAsync("OnColumnResizeEnd", columnIndex, width);
    };

    this._resizeMove = onMove;
    this._resizeUp = onUp;
    this._resizeCursor = document.body.style.cursor;
    this._resizeUserSelect = document.body.style.userSelect;

    document.body.style.cursor = "col-resize";
    document.body.style.userSelect = "none";
    document.addEventListener("mousemove", onMove);
    document.addEventListener("mouseup", onUp);
  },

  startRowResize: function (dotNetRef, rowIndex, startClientY, startHeight) {
    this._stopResize();

    const onMove = (e) => {
      e.preventDefault();
      const height = Math.max(18, startHeight + (e.clientY - startClientY));
      dotNetRef.invokeMethodAsync("OnRowResizeMove", rowIndex, height);
    };

    const onUp = (e) => {
      e.preventDefault();
      this._stopResize();
      const height = Math.max(18, startHeight + (e.clientY - startClientY));
      dotNetRef.invokeMethodAsync("OnRowResizeEnd", rowIndex, height);
    };

    this._resizeMove = onMove;
    this._resizeUp = onUp;
    this._resizeCursor = document.body.style.cursor;
    this._resizeUserSelect = document.body.style.userSelect;

    document.body.style.cursor = "row-resize";
    document.body.style.userSelect = "none";
    document.addEventListener("mousemove", onMove);
    document.addEventListener("mouseup", onUp);
  },

  _stopResize: function () {
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

  writeText: async function (text) {
    try {
      if (navigator.clipboard && navigator.clipboard.writeText) {
        await navigator.clipboard.writeText(text ?? "");
        return true;
      }
    } catch (_) {}

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
    } catch (_) {}
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
