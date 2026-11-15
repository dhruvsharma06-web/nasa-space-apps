mergeInto(LibraryManager.library, {
  // Saves a text file in the browser (session log for the instructor dashboard).
  AstraDownloadText: function (fileNamePtr, textPtr) {
    var name = UTF8ToString(fileNamePtr), text = UTF8ToString(textPtr);
    var blob = new Blob([text], { type: "application/json" });
    var a = document.createElement("a");
    a.href = URL.createObjectURL(blob); a.download = name;
    document.body.appendChild(a); a.click(); document.body.removeChild(a);
    setTimeout(function () { URL.revokeObjectURL(a.href); }, 1000);
  }
});
