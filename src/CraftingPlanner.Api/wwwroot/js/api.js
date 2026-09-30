/* Talking to the backend. Every page uses these functions and nothing else to reach
   the API, so the browser's network panel shows exactly the same calls that the
   interactive API page does. */

/**
 * An error thrown when the API rejects a request. It carries the Problem Details body
 * the API sent, so the page can show the title, the explanation, and the fields at fault.
 */
class ApiError extends Error {
  /**
   * @param {number} status The HTTP status code, such as 404 or 409.
   * @param {object} problem The Problem Details body: title, detail, errors, and any extra fields.
   */
  constructor(status, problem) {
    super(problem.title || `Request failed with status ${status}`);
    this.status = status;
    this.problem = problem;
  }
}

/**
 * Sends one request to the API.
 * @param {string} method GET, POST, PUT, PATCH, or DELETE.
 * @param {string} path The address after the site's root, such as "/api/items/9".
 * @param {object} [body] JSON to send. Left out for GET and DELETE.
 * @returns {Promise<any>} The parsed JSON response, or null for a response with no body.
 * @throws {ApiError} When the API answers with a rejection.
 */
async function request(method, path, body) {
  const options = { method, headers: { Accept: "application/json" } };
  if (body !== undefined) {
    options.headers["Content-Type"] = "application/json";
    options.body = JSON.stringify(body);
  }

  let response;
  try {
    response = await fetch(path, options);
  } catch {
    throw new ApiError(0, {
      title: "The server could not be reached",
      detail: "Check that the app is running, then try again.",
    });
  }

  if (response.status === 204) {
    return null;
  }

  const text = await response.text();
  const data = text ? JSON.parse(text) : null;

  if (!response.ok) {
    throw new ApiError(response.status, data || { title: `Request failed with status ${response.status}` });
  }

  return data;
}

/** The API, one function per kind of request. */
const api = {
  /** @param {string} path Address to read. */
  get: (path) => request("GET", path),
  /** @param {string} path Address to create at. @param {object} body The new record. */
  post: (path, body) => request("POST", path, body),
  /** @param {string} path Address to replace. @param {object} body The full record. */
  put: (path, body) => request("PUT", path, body),
  /** @param {string} path Address to change. @param {object} body Only the fields to change. */
  patch: (path, body) => request("PATCH", path, body),
  /** @param {string} path Address to delete. */
  delete: (path) => request("DELETE", path),
};

/**
 * Shows a rejection in a notice element: the title, the explanation, and each field at fault.
 * @param {HTMLElement} notice The element with the "notice" class.
 * @param {Error} error The error, usually an ApiError.
 */
function showProblem(notice, error) {
  const problem = error instanceof ApiError ? error.problem : { title: "Something went wrong", detail: error.message };

  notice.replaceChildren();
  notice.className = "notice error";
  notice.appendChild(el("strong", {}, problem.title || "Request failed"));

  if (problem.detail) {
    notice.appendChild(el("div", {}, problem.detail));
  }

  if (problem.errors) {
    const list = el("ul");
    for (const [field, messages] of Object.entries(problem.errors)) {
      for (const message of messages) {
        list.appendChild(el("li", {}, `${field}: ${message}`));
      }
    }
    notice.appendChild(list);
  }
}

/**
 * Shows a short confirmation in a notice element.
 * @param {HTMLElement} notice The element with the "notice" class.
 * @param {string} text What happened.
 */
function showSuccess(notice, text) {
  notice.replaceChildren(el("div", {}, text));
  notice.className = "notice success";
}

/**
 * Hides a notice element.
 * @param {HTMLElement} notice The element with the "notice" class.
 */
function clearNotice(notice) {
  notice.replaceChildren();
  notice.className = "notice";
}
