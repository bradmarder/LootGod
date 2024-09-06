import { CanceledError } from "axios";

export const swallowAbortError = (error: unknown) => {
	if (error instanceof CanceledError) { return; }
	throw error;
};
