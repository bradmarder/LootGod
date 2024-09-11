import { AxiosError, CanceledError } from "axios";
import Swal from "sweetalert2";

export const swallowAbortError = (error: unknown) => {
	if (error instanceof CanceledError && error.config?.signal?.aborted) { return; }
	throw error;
};

export const handlePostError = (error: unknown) => {
	if (error instanceof AxiosError) {
		throw Swal.fire({
			icon: 'error',
			title: 'Womp Womp!',
			text: error.response?.data || error.response?.statusText || error.message,
		});
	}
	throw error;
};
