(() => {
  const modeCheckboxes = [...document.querySelectorAll(".content-visible-mode")];
  const modeSummary = document.getElementById("visible-mode-summary");
  const updateModeSummary = () => {
    if (!modeSummary) return;
    const selected = modeCheckboxes.filter(input => input.checked).map(input => input.value);
    modeSummary.textContent = selected.length ? selected.join(", ") : "Select visible modes";
  };
  modeCheckboxes.forEach(input => input.addEventListener("change", updateModeSummary));
  updateModeSummary();

  const body = document.getElementById("Document_Body");
  const sourceTab = document.getElementById("content-source-tab");
  const visualTab = document.getElementById("content-visual-tab");
  const editor = document.getElementById("content-visual-editor");
  const surface = document.getElementById("content-visual-surface");
  if (!body || !sourceTab || !visualTab || !editor || !surface) return;

  let visualActive = false;

  const inlineMarkdown = node => {
    if (node.nodeType === Node.TEXT_NODE) {
      return node.nodeValue.replace(/([\\*_[\]])/g, "\\$1");
    }
    if (node.nodeType !== Node.ELEMENT_NODE) return "";
    const element = node;
    const content = [...element.childNodes].map(inlineMarkdown).join("");
    switch (element.tagName.toLowerCase()) {
      case "strong": case "b": return `**${content}**`;
      case "em": case "i": return `*${content}*`;
      case "del": case "s": return `~~${content}~~`;
      case "code": return `\`${element.textContent}\``;
      case "a": return `[${content || element.getAttribute("href")}](${element.getAttribute("href") || ""})`;
      case "img": return `![${element.getAttribute("alt") || ""}](${element.getAttribute("src") || ""})`;
      case "br": return "  \n";
      default: return content;
    }
  };

  const blockMarkdown = (node, depth = 0) => {
    if (node.nodeType === Node.TEXT_NODE) return node.nodeValue.trim() ? `${inlineMarkdown(node)}\n\n` : "";
    if (node.nodeType !== Node.ELEMENT_NODE) return "";
    const element = node;
    const tag = element.tagName.toLowerCase();
    if (element.classList.contains("content-visual-directive")) {
      return `${element.dataset.directive || element.textContent.trim()}\n\n`;
    }
    if (/^h[1-6]$/.test(tag)) return `${"#".repeat(Number(tag[1]))} ${inlineMarkdown(element)}\n\n`;
    if (tag === "p") return `${inlineMarkdown(element).trim()}\n\n`;
    if (tag === "blockquote") {
      return blockChildren(element).trim().split("\n").map(line => `> ${line}`).join("\n") + "\n\n";
    }
    if (tag === "pre") return `\`\`\`\n${element.textContent.replace(/\n$/, "")}\n\`\`\`\n\n`;
    if (tag === "hr") return "---\n\n";
    if (tag === "ul" || tag === "ol") {
      return [...element.children].filter(child => child.tagName.toLowerCase() === "li").map((item, index) => {
        const clone = item.cloneNode(true);
        clone.querySelectorAll(":scope > ul, :scope > ol").forEach(nested => nested.remove());
        const marker = tag === "ol" ? `${index + 1}.` : "-";
        const nested = [...item.children].filter(child => ["ul", "ol"].includes(child.tagName.toLowerCase()))
          .map(child => blockMarkdown(child, depth + 1).trimEnd().split("\n").map(line => `  ${line}`).join("\n"))
          .join("\n");
        return `${marker} ${inlineMarkdown(clone).trim()}${nested ? `\n${nested}` : ""}`;
      }).join("\n") + "\n\n";
    }
    if (tag === "table") {
      const rows = [...element.querySelectorAll("tr")].map(row =>
        [...row.querySelectorAll(":scope > th, :scope > td")].map(cell => inlineMarkdown(cell).trim().replace(/\|/g, "\\|")));
      if (!rows.length) return "";
      const width = Math.max(...rows.map(row => row.length));
      const normalized = rows.map(row => [...row, ...Array(width - row.length).fill("")]);
      return `${normalized.map(row => `| ${row.join(" | ")} |`).slice(0, 1).join("\n")}\n| ${Array(width).fill("---").join(" | ")} |\n${normalized.slice(1).map(row => `| ${row.join(" | ")} |`).join("\n")}\n\n`;
    }
    if (tag === "img") return `${inlineMarkdown(element)}\n\n`;
    return blockChildren(element);
  };

  const blockChildren = element => [...element.childNodes].map(node => blockMarkdown(node)).join("");
  const syncMarkdown = () => {
    if (visualActive) body.value = blockChildren(surface).replace(/\n{3,}/g, "\n\n").trimEnd();
  };

  const showSource = () => {
    syncMarkdown();
    visualActive = false;
    body.classList.remove("d-none");
    editor.classList.add("d-none");
    sourceTab.classList.add("active");
    visualTab.classList.remove("active");
  };

  const showVisual = async () => {
    const token = body.form.querySelector('input[name="__RequestVerificationToken"]')?.value;
    const data = new FormData();
    data.append("body", body.value);
    if (token) data.append("__RequestVerificationToken", token);
    visualTab.disabled = true;
    try {
      const response = await fetch("/editor/content/visual/render", { method: "POST", body: data });
      if (!response.ok) throw new Error("Visual rendering failed.");
      surface.innerHTML = (await response.json()).html;
      visualActive = true;
      body.classList.add("d-none");
      editor.classList.remove("d-none");
      sourceTab.classList.remove("active");
      visualTab.classList.add("active");
      surface.focus();
    } catch (error) {
      window.alert(error.message);
    } finally {
      visualTab.disabled = false;
    }
  };

  sourceTab.addEventListener("click", showSource);
  visualTab.addEventListener("click", showVisual);
  body.form.addEventListener("submit", syncMarkdown);

  editor.querySelectorAll("[data-visual-command]").forEach(button => button.addEventListener("click", () => {
    surface.focus();
    document.execCommand(button.dataset.visualCommand, false);
  }));
  editor.querySelectorAll("[data-visual-block]").forEach(button => button.addEventListener("click", () => {
    surface.focus();
    document.execCommand("formatBlock", false, button.dataset.visualBlock);
  }));
  editor.querySelector('[data-visual-action="link"]')?.addEventListener("click", () => {
    const url = window.prompt("Link destination");
    if (url) document.execCommand("createLink", false, url);
  });
  editor.querySelector('[data-visual-action="image"]')?.addEventListener("click", () => {
    const url = window.prompt("Attached media URL (/content/media/...)");
    if (!url) return;
    const alt = window.prompt("Image description (alt text)") || "";
    document.execCommand("insertHTML", false, `<img src="${url.replace(/"/g, "&quot;")}" alt="${alt.replace(/"/g, "&quot;")}"><br>`);
  });
  editor.querySelector('[data-visual-action="table"]')?.addEventListener("click", () => {
    document.execCommand("insertHTML", false,
      "<table><thead><tr><th>Heading</th><th>Heading</th></tr></thead><tbody><tr><td>Value</td><td>Value</td></tr></tbody></table><p><br></p>");
  });
})();
