import { useState, useEffect } from 'react';
import { Alert, Button, Form } from 'react-bootstrap';
import axios from 'axios';
import Swal from 'sweetalert2/dist/sweetalert2.js';

export default function leaderModule() {

	const [loading, setLoading] = useState(true);
	const [raidDiscord, setRaidDiscord] = useState('');
	const [rotDiscord, setRotDiscord] = useState('');
	const [transferName, setTransferName] = useState('');
	const [messageOfTheDay, setMessageOfTheDay] = useState('');

	const transferLeadership = () => {
		setLoading(true);
		return Swal.fire({
			title: 'Confirmation',
			text: `Are you sure you wish to transfer guild leadership to "${transferName}"? You will still retain admin privileges.`,
			icon: 'warning',
			showCancelButton: true,
			confirmButtonText: 'Yes, Transfer',
		})
		.then(x => x.isConfirmed
			? axios.post('/TransferGuildLeadership', { name: transferName })
			: Promise.reject())
		.then(() => Swal.fire('Transferred', `Successfully transfered guild leadership to "${transferName}"`, 'success'))
		.finally(() => setLoading(false));
	};
	const updateDiscord = () => {
		setLoading(true);
		const a = axios.post('/GuildDiscord', { raidNight: true, webhook: raidDiscord });
		const b = axios.post('/GuildDiscord', { raidNight: false, webhook: rotDiscord });
		Promise
			.all([a, b])
			.then(() => Swal.fire('Discord Webhooks Updated', 'Successfully updated Discord webhooks', 'success'))
			.finally(() => setLoading(false));
	};
	const uploadMessageOfTheDay = () => {
		setLoading(true);
		axios
			.post('/UploadMessageOfTheDay', { message: messageOfTheDay })
			.then(() => Swal.fire('MOTD Updated', 'Successfully updated the MOTD', 'success'))
			.finally(() => setLoading(false));
	};
	useEffect(() => {
		const ac = new AbortController();
		const { signal } = ac;
		const a = axios
			.get<string>('/GetMessageOfTheDay', { signal })
			.then(x => setMessageOfTheDay(x.data));
		const b = axios
			.get<IDiscordWebhooks>('/GetDiscordWebhooks', { signal })
			.then(x => {
				setRaidDiscord(x.data.raid);
				setRotDiscord(x.data.rot);
			});
		Promise
			.all([a, b])
			.finally(() => setLoading(false));
		return () => ac.abort();
	}, []);

	return (
		<Alert variant='dark'>
			<Form onSubmit={e => e.preventDefault()}>
				<Form.Group>
					<Form.Label>Guild Message of the Day</Form.Label>
					<Form.Control as='textarea' rows={3} placeholder='Enter guild MOTD' value={messageOfTheDay} onChange={e => setMessageOfTheDay(e.target.value)} />
				</Form.Group>
				<br />
				<Button variant='primary' disabled={loading} onClick={uploadMessageOfTheDay}>Update</Button>
			</Form>
			<hr />
			<Form onSubmit={e => e.preventDefault()}>
				<Form.Group>
					<Form.Label>Transfer Guild Leadership</Form.Label>
					<Form.Control type='text' placeholder='Enter new guild leader name' value={transferName} onChange={e => setTransferName(e.target.value)} />
				</Form.Group>
				<br />
				<Button variant='warning' disabled={loading || transferName.length < 4} onClick={transferLeadership}>Transfer</Button>
			</Form>
			<hr />
			<Form onSubmit={e => e.preventDefault()}>
				<Form.Group>
					<Form.Label>Raid Discord Webhook URL</Form.Label>
					<Form.Control type='text' placeholder='Enter Raid Discord webhook URL' value={raidDiscord} onChange={e => setRaidDiscord(e.target.value)} />
				</Form.Group>
				<hr />
				<Form.Group>
					<Form.Label>Rot Discord Webhook URL</Form.Label>
					<Form.Control type='text' placeholder='Enter Rot Discord webhook URL' value={rotDiscord} onChange={e => setRotDiscord(e.target.value)} />
				</Form.Group>
				<br />
				<Button variant='warning' disabled={loading} onClick={updateDiscord}>Update</Button>
			</Form>
		</Alert>
	);
}