import { API_BASE } from "../api.constant";
import type { ILoginResponse } from "./login-response.model";
import {ILoginRequest} from "@/src/lib/api/auth/login-request.model";

export async function login(request: ILoginRequest): Promise<ILoginResponse> {
    const res = await fetch(`${API_BASE}/auth/login`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(request),
    });

    if (!res.ok) {
        const problem = await res.json().catch(() => ({}));
        throw new Error(problem.detail ?? "Login failed");
    }

    return res.json();
}