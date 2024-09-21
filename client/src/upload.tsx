import { useState } from 'react';
import { Alert, Form } from 'react-bootstrap';
import axios from 'axios';
import Swal from 'sweetalert2/dist/sweetalert2.js';

export default function Upload(props: { refreshCache: () => void }) {

	const [loading, setLoading] = useState(false);
	const [key, setKey] = useState(1);
	const uploadDump = (file: File) => {
		setLoading(true);
		const formData = new FormData();
		const offset = new Date().getTimezoneOffset();
		formData.append("file", file);
		axios
			.post('/ImportDump?offset=' + offset, formData)
			.then(() => props.refreshCache())
			.then(() => Swal.fire('Upload Success', `Successfully uploaded the file "${file.name}"`, 'success'))
			.finally(() => {
				setKey(x => x + 1);
				setLoading(false);
			});
	};
	const passwordHref = '/api/GetPasswords?' + new URLSearchParams({
		playerKey: localStorage.getItem('key')!,
	}).toString();

	return (
		<Alert variant='primary'>
			<h4>Upload Guild/Raid Dumps (Admin only)</h4>
			<hr />
			<Alert variant='info'>NOTE! It is safe to upload duplicate raid dumps - the system automatically deduplicates</Alert>
			<hr />
			<Form>
				<input type='file' key={key} accept='.txt,.zip' disabled={loading} onChange={e => uploadDump(e.target.files![0]!)} />
				<hr />
				<a target="_blank" rel="noreferrer" href={passwordHref}>Download Master Password Links (Leader Only)</a>
			</Form>
		</Alert>
	);
}