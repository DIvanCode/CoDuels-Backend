const kpiIds = ["startedRate", "finishedRate", "pickRate", "durationP95", "priorityP95", "workerCount"];

initializeTimeRangeInputs();
syncTimeRangeHiddenInputs();
document.getElementById("timeRangeForm").addEventListener("submit", syncTimeRangeHiddenInputs);
tick();

async function tick() {
  const url = apiHistoryUrl();
  if (!url) {
    document.getElementById("status").textContent = "Select start and end time.";
    return;
  }
  try {
    const response = await fetch(url, { cache: "no-store" });
    if (!response.ok) {
      throw new Error(`request failed: ${response.status}`);
    }
    render(await response.json());
  } catch (error) {
    document.getElementById("status").innerHTML = `<span class="bad">${escapeHtml(error.message)}</span>`;
  }
}

function apiHistoryUrl() {
  const params = new URLSearchParams(window.location.search);
  const start = params.get("start") || localInputToIsoWithOffset(document.getElementById("startInput").value);
  const end = params.get("end") || localInputToIsoWithOffset(document.getElementById("endInput").value);
  if (!start || !end) {
    return "";
  }
  params.set("start", start);
  params.set("end", end);
  return `api/history/?${params.toString()}`;
}

function initializeTimeRangeInputs() {
  const params = new URLSearchParams(window.location.search);
  const end = new Date();
  const start = new Date(end.getTime() - 30 * 60 * 1000);
  document.getElementById("startInput").value = params.get("start") ? formatDatetimeLocal(new Date(params.get("start"))) : formatDatetimeLocal(start);
  document.getElementById("endInput").value = params.get("end") ? formatDatetimeLocal(new Date(params.get("end"))) : formatDatetimeLocal(end);
}

function syncTimeRangeHiddenInputs() {
  document.getElementById("startHidden").value = localInputToIsoWithOffset(document.getElementById("startInput").value);
  document.getElementById("endHidden").value = localInputToIsoWithOffset(document.getElementById("endInput").value);
}

function formatDatetimeLocal(date) {
  const pad = (value) => String(value).padStart(2, "0");
  return [
    date.getFullYear(),
    pad(date.getMonth() + 1),
    pad(date.getDate()),
  ].join("-") + `T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

function localInputToIsoWithOffset(value) {
  if (!value) {
    return "";
  }
  return dateToIsoWithOffset(new Date(value));
}

function dateToIsoWithOffset(date) {
  const pad = (value) => String(Math.trunc(Math.abs(value))).padStart(2, "0");
  const offsetMinutes = -date.getTimezoneOffset();
  const sign = offsetMinutes >= 0 ? "+" : "-";
  const hours = pad(offsetMinutes / 60);
  const minutes = pad(offsetMinutes % 60);
  return `${formatDatetimeLocal(date)}:00${sign}${hours}:${minutes}`;
}

function render(payload) {
  document.getElementById("status").innerHTML = payload.status || "No data.";
  for (const id of kpiIds) {
    document.getElementById(id).textContent = payload.kpis?.[id] || "0";
  }
  setHtml("throughputChart", payload.charts?.throughput);
  setHtml("fairnessChart", payload.charts?.fairness);
  setHtml("executionPriorityChart", payload.charts?.execution_priority);
  setHtml("executionProgressChart", payload.charts?.execution_progress);
  setHtml("slotChart", payload.charts?.worker_slots);
  setHtml("memoryChart", payload.charts?.worker_memory);
  setHtml("rectangles", payload.charts?.rectangles);
  setHtml("workerPool", payload.tables?.worker_pool);
  setHtml("workers", payload.tables?.workers);
}

function setHtml(id, html) {
  document.getElementById(id).innerHTML = html || "";
}

function escapeHtml(value) {
  return String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
}
