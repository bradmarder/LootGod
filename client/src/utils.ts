import axios, { AxiosError } from "axios";

// use explicit ".js" version because it does not contain inline-svgs that conflict with CSP policy
import Swal from "sweetalert2/dist/sweetalert2.js";

export const setAxiosInterceptors = () => {
	axios.interceptors.response.use(
		x => x,
		async error => {

			// catch abort errors and throw null rejection to avoid unhandledrejection error page
			if (axios.isCancel(error)) {
				return Promise.reject();
			}

			console.error(error);

			if (error instanceof AxiosError && ['post', 'delete'].includes(error.config?.method ?? '')) {
				await Swal.fire({
					icon: 'error',
					title: error.code,
					text: error.response?.data || error.response?.statusText || error.message,
				});
			}

			return Promise.reject(error);
		}
	);
};

export const toast = (title: string) => Swal.fire({
	title: title,
	icon: 'success',
	toast: true,
	position: 'top-end',
	showConfirmButton: false,
	timer: 3000,
	timerProgressBar: true,
	didOpen: (toast) => {
		toast.onmouseenter = Swal.stopTimer;
		toast.onmouseleave = Swal.resumeTimer;
	},
});
