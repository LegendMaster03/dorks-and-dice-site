(() => {
  const source = document.getElementById("Document_MetadataJson");
  const standardTab = document.getElementById("metadata-standard-tab");
  const sourceTab = document.getElementById("metadata-source-tab");
  const standardEditor = document.getElementById("metadata-standard-editor");
  const sourceEditor = document.getElementById("metadata-source-editor");
  if (!source || !standardTab || !sourceTab || !standardEditor || !sourceEditor) return;

  const fields = [...standardEditor.querySelectorAll("[data-meta-path]")];
  let metadata;
  let standardActive = true;

  const getValue = (object, path) => path.split(".").reduce((value, key) => value?.[key], object);
  const setValue = (object, path, value, required) => {
    const parts = path.split(".");
    const key = parts.pop();
    let target = object;
    const remove = (value === "" || value === null) && !required;
    for (const part of parts) {
      if (remove && (!target[part] || typeof target[part] !== "object")) return;
      target = target[part] ??= {};
    }
    if (remove) delete target[key];
    else target[key] = value;
  };

  const parseSource = () => {
    try {
      metadata = JSON.parse(source.value || "{}");
      if (!metadata || Array.isArray(metadata) || typeof metadata !== "object") throw new Error();
      return true;
    } catch {
      window.alert("Metadata Source must contain a valid JSON object before switching to Standard mode.");
      return false;
    }
  };

  const populate = () => fields.forEach(field => {
    const value = getValue(metadata, field.dataset.metaPath);
    if (field.type === "checkbox") field.checked = value === true;
    else if (field.dataset.metaNullableBool === "true") field.value = value === true ? "true" : value === false ? "false" : "";
    else if (field.dataset.metaList === "true") field.value = Array.isArray(value) ? value.join("\n") : "";
    else field.value = value ?? "";
  });

  const sync = () => {
    fields.forEach(field => {
      let value;
      if (field.type === "checkbox") value = field.checked;
      else if (field.dataset.metaNullableBool === "true") value = field.value === "" ? null : field.value === "true";
      else if (field.dataset.metaList === "true") value = field.value.split(/\r?\n/).map(item => item.trim()).filter(Boolean);
      else if (field.dataset.metaNumber === "true") value = field.value === "" ? null : Number(field.value);
      else value = field.value.trim();
      setValue(metadata, field.dataset.metaPath, value, field.dataset.metaRequired === "true");
    });
    source.value = JSON.stringify(metadata, null, 2);
  };

  const showStandard = () => {
    if (!parseSource()) return;
    populate();
    standardActive = true;
    standardEditor.classList.remove("d-none");
    sourceEditor.classList.add("d-none");
    standardTab.classList.add("active");
    sourceTab.classList.remove("active");
  };

  const showSource = () => {
    if (standardActive) sync();
    standardActive = false;
    standardEditor.classList.add("d-none");
    sourceEditor.classList.remove("d-none");
    standardTab.classList.remove("active");
    sourceTab.classList.add("active");
  };

  standardTab.addEventListener("click", showStandard);
  sourceTab.addEventListener("click", showSource);
  source.form.addEventListener("submit", () => { if (standardActive) sync(); });
  showStandard();
})();
