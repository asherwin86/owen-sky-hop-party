// Minimal browser WebSocket bridge. C# polls it every frame (no callbacks into C#).
mergeInto(LibraryManager.library, {
  HOP_Connect: function (urlPtr) {
    var url = UTF8ToString(urlPtr);
    if (window.__hop && window.__hop.ws) { try { window.__hop.ws.close(); } catch (e) {} }
    var w = window.__hop = { ws: null, queue: [], state: 0 };
    try {
      var ws = new WebSocket(url);
      w.ws = ws;
      ws.onopen = function () { if (window.__hop === w) w.state = 1; };
      ws.onmessage = function (e) { if (window.__hop === w) w.queue.push(String(e.data)); };
      ws.onclose = function () { if (window.__hop === w) w.state = 3; };
      ws.onerror = function () { if (window.__hop === w) w.state = 3; };
    } catch (e) { w.state = 3; }
  },
  HOP_State: function () {
    return window.__hop ? window.__hop.state : 3;
  },
  HOP_Send: function (msgPtr) {
    var w = window.__hop;
    if (w && w.state === 1) { try { w.ws.send(UTF8ToString(msgPtr)); } catch (e) {} }
  },
  HOP_Recv: function () {
    var w = window.__hop;
    if (!w || w.queue.length === 0) return 0;
    var s = w.queue.shift();
    var len = lengthBytesUTF8(s) + 1;
    var buf = _malloc(len);
    stringToUTF8(s, buf, len);
    return buf;
  },
  HOP_Close: function () {
    var w = window.__hop;
    if (w && w.ws) { try { w.ws.close(); } catch (e) {} }
    window.__hop = null;
  }
});
