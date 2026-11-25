export function tableText(value) {
    return String(value ?? "").replaceAll("&", "&amp;").replaceAll("<", "&lt;").replaceAll(">", "&gt;")
        .replaceAll('"', "&quot;").replace(/\r?\n/g, "<br>");
}

export function reportTable(headers, rows, { numeric = [], footer, widths = [] } = {}) {
    const numericColumns = new Set(numeric);
    const style = "border:1px solid #d8e0e7;padding:11px 12px;vertical-align:top;";
    const heading = headers.map((value, index) => `<th scope="col" align="${numericColumns.has(index) ? "right" : "left"}" style="${style}background-color:#edf2f6;color:#445361;${widths[index] ? `width:${widths[index]};` : ""}">${tableText(value)}</th>`).join("");
    const cells = (row, total = false) => row.map((value, index) => `<td align="${numericColumns.has(index) ? "right" : "left"}" style="${style}${total ? "background-color:#f3f6f8;" : ""}">${total ? `<strong>${value}</strong>` : value}</td>`).join("");
    return `<div style="overflow-x:auto"><table border="1" cellpadding="10" cellspacing="0" width="100%" style="border-collapse:collapse;font-size:12px;width:100%;"><thead><tr>${heading}</tr></thead><tbody>${rows.map((row, index) => `<tr${index % 2 ? ' bgcolor="#f8fafc"' : ""}>${cells(row)}</tr>`).join("")}</tbody>${footer ? `<tfoot><tr>${cells(footer, true)}</tr></tfoot>` : ""}</table></div>`;
}
