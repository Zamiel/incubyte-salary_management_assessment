import { API_BASE } from "./api.constant";
import { getToken, clearToken } from "./token.util";

export async function fetchWithAuth<T>(
    path: string,
    options: RequestInit = {}
): Promise<T> {
    const token = getToken();
    const headers = new Headers(options.headers);
    if (token) headers.set("Authorization", `Bearer ${token}`);

    const res = await fetch(`${API_BASE}${path}`, { ...options, headers });

    if (res.status === 401) {
        clearToken();
        throw new Error("Unauthorized");
    }

    if (!res.ok) {
        const problem = await res.json().catch(() => ({}));
        throw new Error(problem.detail ?? `Request failed: ${res.status}`);
    }

    return res.json();
}