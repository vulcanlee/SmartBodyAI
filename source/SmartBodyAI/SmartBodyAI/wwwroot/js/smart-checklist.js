window.smartChecklist = {
  loadFromLocalStorage(key) {
    return window.localStorage.getItem(key);
  },

  saveToLocalStorage(key, value) {
    window.localStorage.setItem(key, value);
  },

  removeFromLocalStorage(key) {
    window.localStorage.removeItem(key);
  },

  downloadJson(filename, content) {
    const blob = new Blob([content], { type: "application/json;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");

    link.href = url;
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
  }
};
