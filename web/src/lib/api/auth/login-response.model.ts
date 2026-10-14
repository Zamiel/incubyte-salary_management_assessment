import { IAuthenticatedUser } from "./authenticated-user.model";

export interface ILoginResponse {
    accessToken: string;
    expiresAt: string;
    user: IAuthenticatedUser;
}